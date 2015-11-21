using System;
using System.Collections.Generic;
using Windows.UI;
using Windows.UI.Xaml.Controls;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Windows.UI.Xaml.Input;
using Windows.UI.Input;
using System.Numerics;
using Windows.UI.Xaml;
using System.Diagnostics;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace WinNetSty {

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
        public int Button { get; set; }

        public InkButtonEventArgs(PointerPoint point, Control container) : base(point, container) {
            if (point.Properties.IsBarrelButtonPressed) {
                this.Button = 2;
            } else if (point.Properties.IsEraser) {
                this.Button = 1;
            } else {
                this.Button = 0;
            }
        }
    }

    public class InkButtonDownEventArgs : InkButtonEventArgs {
        public InkButtonDownEventArgs(PointerPoint point, Control container) : base(point, container) { }
    }
    public class InkButtonUpEventArgs : InkButtonEventArgs {
        public InkButtonUpEventArgs(PointerPoint point, Control container) : base(point, container) { }
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

        public delegate void InkDownEventHandler(DrawCanvas sender, InkButtonDownEventArgs e);
        public delegate void InkMoveEventHandler(DrawCanvas sender, InkEventArgs e);
        public delegate void InkUpEventHandler(DrawCanvas sender, InkButtonUpEventArgs e);

        public event InkDownEventHandler InkDown;
        public event InkMoveEventHandler InkMove;
        public event InkUpEventHandler InkUp;

        private Settings settings;
        private DispatcherTimer redrawTimer;
        bool drawing;

        public DrawCanvas() {
            this.InitializeComponent();
            points = new Queue<DrawnPoint>();
            settings = WinNetStyApp.Current.Settings;
            redrawTimer = new DispatcherTimer();
            redrawTimer.Interval = new TimeSpan(0, 0, 0, 0, 1000/60);
            redrawTimer.Tick += RedrawScreen;
            drawing = false;
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


        protected override void OnPointerPressed(PointerRoutedEventArgs e) {
            Pointer pointer = e.Pointer;
            if (!weCareAbout(pointer))
                return;
            PointerPoint point = e.GetCurrentPoint(this.canvas);
            e.Handled = true;
            CapturePointer(pointer);

            points.Enqueue(new DrawnPoint(point));
            MarkCanvasDirty();

            InkDown?.Invoke(this, new InkButtonDownEventArgs(point, this.canvas));
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

            InkUp?.Invoke(this, new InkButtonUpEventArgs(point, this.canvas));
        }

        protected override void OnPointerMoved(PointerRoutedEventArgs e) {
            Pointer pointer = e.Pointer;
            if (!weCareAbout(pointer))
                return;
            PointerPoint point = e.GetCurrentPoint(this.canvas);
            
            points.Enqueue(new DrawnPoint(point));
            MarkCanvasDirty();

            InkMove?.Invoke(this, new InkButtonEventArgs(point, this.canvas));
        }
        
        
    }
}
