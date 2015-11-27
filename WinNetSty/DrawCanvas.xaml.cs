using System;
using System.Collections.Generic;
using Windows.UI;
using Windows.UI.Xaml.Controls;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Windows.UI.Xaml.Input;
using Windows.UI.Input;
using System.Numerics;
using Windows.UI.Xaml;
using System.Windows.Input;
using System.Diagnostics;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace WinNetSty {

    public enum ButtonStatus : byte {
        Up = 0x00,
        Down = 0x01
    }

    public enum ButtonType : sbyte {
        InRange = -1,
        InContact = 0,
        Eraser = 1,
        Button2 = 2
    }

    enum StylusRangeStatus: byte {
        OutOfRange,
        InRange,
        FakingInRange
    }

    public class InkEventArgs : EventArgs {

        public Vector2 Position {get; set;}
        public float? Pressure { get; set; }

        public InkEventArgs(PointerPoint point, Control container) {
            this.Position = new Vector2((float) (point.Position.X / container.ActualWidth), (float) (point.Position.Y / container.ActualHeight));
            if (point.PointerDevice.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Pen) {
                this.Pressure = point.Properties.Pressure;
            } else {
                this.Pressure = null;
            }
        }
    }

    public class InkButtonEventArgs : InkEventArgs {
        public ButtonType Button { get; set; }
        public ButtonStatus ButtonStatus { get; set; }

        public InkButtonEventArgs(ButtonType button, ButtonStatus buttonStatus, PointerPoint point, Control container) : base(point, container) {
            this.ButtonStatus = buttonStatus;
            this.Button = button;
        }
    }
    
    struct DrawnPoint {

        public long Time { get; set; }
        public bool EndStroke { get; set; }
        public Vector2 Position { get; set; }
        public float? Pressure { get; set; }
        public bool InContact { get; set; }

        public long Age {
            get {
                return DateTime.Now.Ticks - this.Time;
            }
        }

        public DrawnPoint(PointerPoint point, bool endStroke = false) {
            Position = point.Position.ToVector2();
            EndStroke = endStroke;
            Time = DateTime.Now.Ticks;
            InContact = point.IsInContact;
            if (InContact && point.PointerDevice.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Pen) {
                Pressure = point.Properties.Pressure;
            } else {
                Pressure = null;
            }
        }


    }

    public sealed partial class DrawCanvas : UserControl {

        private Queue<DrawnPoint> points;

        public const float baseWidth = 2.0f;
        public const float decayRate = 0.0000001f;
        public const float alphaToCull = 0.03f;
        public readonly float ageToCull = (long)-Math.Log(alphaToCull / (1+alphaToCull) ) / decayRate;

        public delegate void InkButtonEventHandler(DrawCanvas sender, InkButtonEventArgs e);
        public delegate void InkMoveEventHandler(DrawCanvas sender, InkEventArgs e);

        public event InkButtonEventHandler InkButton;
        public event InkMoveEventHandler InkMove;

        private Settings settings;
        private DispatcherTimer redrawTimer;
        bool drawing;
        private ButtonType? currentButton;
        private StylusRangeStatus stylusRangeStatus;

        public DrawCanvas() {
            this.InitializeComponent();
            points = new Queue<DrawnPoint>();
            settings = WinNetStyApp.Current.Settings;
            redrawTimer = new DispatcherTimer();
            redrawTimer.Interval = new TimeSpan(0, 0, 0, 0, 1000/60);
            redrawTimer.Tick += RedrawScreen;
            drawing = false;
            currentButton = null;
            stylusRangeStatus = StylusRangeStatus.OutOfRange;
        }

        private void MarkCanvasDirty() {
            RedrawScreen();
        }
        
        private void RedrawScreen(object sender=null, object e=null) {
            cullPoints();
            canvas.Invalidate();
        }

        private void cullPoints() {
            while ((points.Count > 0) && points.Peek().Age  > ageToCull) {
                points.Dequeue();
            }
        }

        private Boolean weCareAbout(Pointer pointer) {
            switch (pointer.PointerDeviceType) {
                case Windows.Devices.Input.PointerDeviceType.Mouse:
                    return settings.EnableMouse;
                case Windows.Devices.Input.PointerDeviceType.Pen:
                    return settings.EnablePen;
                case Windows.Devices.Input.PointerDeviceType.Touch:
                    return settings.EnableTouch;
                default:
                    return true;
            }
        }


        private void onCanvasDraw(CanvasControl sender, CanvasDrawEventArgs args) {
            
            
            if (drawing) {
                args.DrawingSession.DrawCircle(new Vector2(500,500), 20, Colors.Black);
                return;
            }

            drawing = true;
            redrawTimer.Stop();

            DrawnPoint? lastPoint = null;

            foreach (DrawnPoint p in points) {
                if (lastPoint.HasValue) {
                    DrawnPoint oldPoint = lastPoint.Value;
                    
                    float alpha = (float) Math.Exp(-(oldPoint.Age)*decayRate) * 1.1f - 0.1f;
                    alpha = Math.Max(Math.Min(alpha, 1.0f), 0.0f);

                    Color color = (oldPoint.InContact) ? Colors.Black : Colors.LightGray;

                    args.DrawingSession.DrawLine(
                        point0: oldPoint.Position,
                        point1: p.Position,
                        color: color.withAlpha(alpha),
                        strokeWidth:
                            oldPoint.Pressure.HasValue
                                ? lastPoint.Value.Pressure.Value * baseWidth
                                : baseWidth
                    );
                }
                lastPoint = p;
            }
            if (points.Count > 0) {
                redrawTimer.Start();
            }
            drawing = false;
        }

        private void debugPoint(PointerPoint point) {
            Debug.WriteLine("update type: {0}, button: {1}, eraser: {2}, contact: {3}, pressure: {4}", point.Properties.PointerUpdateKind, point.Properties.IsBarrelButtonPressed, point.Properties.IsEraser, point.IsInContact, point.Properties.Pressure);


        }

        private void UnMangleStylusDown(PointerPoint point, CanvasControl canvas) {

            ButtonType? extraButton = null;

            if (point.PointerDevice.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Pen) {
                if (point.Properties.IsEraser) {
                    extraButton = ButtonType.Eraser;
                } else if (point.Properties.IsBarrelButtonPressed) {
                    extraButton = ButtonType.Button2;
                }
            } else if (point.PointerDevice.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse) {
                if (point.Properties.IsRightButtonPressed) {
                    extraButton = ButtonType.Button2;
                } else if (point.Properties.IsMiddleButtonPressed) {
                    extraButton = ButtonType.Eraser;
                }
            }
            this.currentButton = extraButton;

            // on the PC we are drawing to, In Range events (BTN_TOOL_PEN) need to be received for Touch events (BTN_TOUCH) to be taken notice of.
            if (stylusRangeStatus != StylusRangeStatus.InRange) {
                stylusRangeStatus = StylusRangeStatus.FakingInRange;
                InkButton.Invoke(this, new InkButtonEventArgs(ButtonType.InRange, ButtonStatus.Down, point, this.canvas));
            }

            if (extraButton.HasValue) {
                InkButton?.Invoke(this, new InkButtonEventArgs(extraButton.Value, ButtonStatus.Down, point, this.canvas));
            } else {
                InkButton?.Invoke(this, new InkButtonEventArgs(ButtonType.InContact, ButtonStatus.Down, point, this.canvas));
            }

        }

        private void UnMangleStylusUp(PointerPoint point, CanvasControl canvas) {
            
            if (this.currentButton.HasValue) {
                InkButton?.Invoke(this, new InkButtonEventArgs(currentButton.Value, ButtonStatus.Up, point, this.canvas));
                this.currentButton = null;
            } else {
                InkButton?.Invoke(this, new InkButtonEventArgs(ButtonType.InContact, ButtonStatus.Up, point, this.canvas));
            }

            if (stylusRangeStatus == StylusRangeStatus.FakingInRange) {
                stylusRangeStatus = StylusRangeStatus.OutOfRange;
                InkButton.Invoke(this, new InkButtonEventArgs(ButtonType.InRange, ButtonStatus.Up, point, this.canvas));
            }

        }

        protected override void OnPointerEntered(PointerRoutedEventArgs e) {
            Pointer pointer = e.Pointer;
            if (!weCareAbout(pointer))
                return;

            PointerPoint point = e.GetCurrentPoint(this.canvas);
            e.Handled = true;
            
            InkButton?.Invoke(this, new InkButtonEventArgs(ButtonType.InRange, ButtonStatus.Down, point, this.canvas));
            stylusRangeStatus = StylusRangeStatus.InRange;
        }

        protected override void OnPointerExited(PointerRoutedEventArgs e) {
            Pointer pointer = e.Pointer;
            if (!weCareAbout(pointer))
                return;

            PointerPoint point = e.GetCurrentPoint(this.canvas);
            e.Handled = true;
            
            InkButton?.Invoke(this, new InkButtonEventArgs(ButtonType.InRange, ButtonStatus.Up, point, this.canvas));
            stylusRangeStatus = StylusRangeStatus.OutOfRange;
        }

        protected override void OnPointerPressed(PointerRoutedEventArgs e) {
            Pointer pointer = e.Pointer;
            if (!weCareAbout(pointer))
                return;
            PointerPoint point = e.GetCurrentPoint(this.canvas);
            e.Handled = true;
            CapturePointer(pointer);

            points.Enqueue(new DrawnPoint(point));
            MarkCanvasDirty();

            UnMangleStylusDown(point, this.canvas);
        }

        protected override void OnPointerReleased(PointerRoutedEventArgs e) {
            Pointer pointer = e.Pointer;
            if (!weCareAbout(pointer))
                return;
            PointerPoint point = e.GetCurrentPoint(this.canvas);
            e.Handled = true;
            ReleasePointerCapture(e.Pointer);

            points.Enqueue(new DrawnPoint(point, endStroke: true));
            MarkCanvasDirty();

            UnMangleStylusUp(point, this.canvas);
        }

        protected override void OnPointerMoved(PointerRoutedEventArgs e) {
            Pointer pointer = e.Pointer;
            if (!weCareAbout(pointer))
                return;
            PointerPoint point = e.GetCurrentPoint(this.canvas);
            
            points.Enqueue(new DrawnPoint(point));
            MarkCanvasDirty();
            
            InkMove?.Invoke(this, new InkEventArgs(point, this.canvas));
        }
        
        
        
    }
}
