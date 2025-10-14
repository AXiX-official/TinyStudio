using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace TinyStudio.Views;

public partial class ZoomableImageView : UserControl
{
    private readonly Image _image;
    private readonly TextBlock _infoTextBlock;
    private readonly Button _resetButton;
    private readonly Border _checkerboard;

    private Point _panStart;
    private bool _isPanning;
    private readonly MatrixTransform _matrixTransform = new(Matrix.Identity);

    public ZoomableImageView()
    {
        InitializeComponent();
        _image = this.FindControl<Image>("Image")!;
        _infoTextBlock = this.FindControl<TextBlock>("InfoTextBlock")!;
        _resetButton = this.FindControl<Button>("ResetButton")!;
        _checkerboard = this.FindControl<Border>("Checkerboard")!;

        _image.RenderTransform = _matrixTransform;

        _checkerboard.Background = CreateCheckerboardBrush();
        
        PointerWheelChanged += OnPointerWheelChanged;
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        _resetButton.Click += (s, e) => Reset();
    }

    public void SetImage(Bitmap source)
    {
        _image.Source = source;
        Reset();
    }
    
    public void SetInfo(string info)
    {
        _infoTextBlock.Text = info;
        Reset();
    }

    private void Reset()
    {
        _matrixTransform.Matrix = Matrix.Identity;
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        var point = e.GetPosition(this);
        var zoomFactor = e.Delta.Y > 0 ? 1.1 : 1 / 1.1;
        
        var matrix = _matrixTransform.Matrix;
        
        if (matrix.M11 * zoomFactor < 0.1) // Prevent zooming out too much
        {
            return;
        }
        
        var scale = Matrix.CreateScale(zoomFactor, zoomFactor);
        var translate = Matrix.CreateTranslation(point.X, point.Y);

        matrix = translate * scale * translate.Invert() * matrix;

        _matrixTransform.Matrix = matrix;
        
        e.Handled = true;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _isPanning = true;
            _panStart = e.GetPosition(this);
            this.Cursor = new Cursor(StandardCursorType.Hand);
            e.Handled = true;
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isPanning)
        {
            var currentPos = e.GetPosition(this);
            var delta = currentPos - _panStart;
            _panStart = currentPos;

            var matrix = _matrixTransform.Matrix;
            matrix = Matrix.CreateTranslation(delta.X, delta.Y) * matrix;
            _matrixTransform.Matrix = matrix;
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isPanning)
        {
            _isPanning = false;
            this.Cursor = Cursor.Default;
        }
    }
    
    private static DrawingBrush CreateCheckerboardBrush()
    {
        var drawing = new DrawingGroup
        {
            Children = 
            {
                new GeometryDrawing
                {
                    Brush = Brushes.LightGray,
                    Geometry = new RectangleGeometry(new Rect(0, 0, 20, 20))
                },
                new GeometryDrawing
                {
                    Brush = Brushes.White,
                    Geometry = new GeometryGroup
                    {
                        Children =
                        {
                            new RectangleGeometry(new Rect(0, 0, 10, 10)),
                            new RectangleGeometry(new Rect(10, 10, 10, 10))
                        }
                    }
                }
            }
        };

        return new DrawingBrush
        {
            Drawing = drawing,
            TileMode = TileMode.Tile,
            DestinationRect = new RelativeRect(0, 0, 20, 20, RelativeUnit.Absolute),
            Stretch = Stretch.None
        };
    }
}
