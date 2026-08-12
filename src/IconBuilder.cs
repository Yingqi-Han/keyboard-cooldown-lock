using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;

internal static class IconBuilder
{
    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    private static int Main(string[] args)
    {
        if (args.Length != 1) return 2;

        using (Bitmap bitmap = new Bitmap(64, 64))
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);
            using (Brush background = new SolidBrush(Color.FromArgb(20, 184, 166)))
                graphics.FillEllipse(background, 1, 1, 62, 62);
            using (Pen shackle = new Pen(Color.White, 7))
                graphics.DrawArc(shackle, 20, 12, 24, 30, 180, -180);
            using (Brush body = new SolidBrush(Color.White))
                graphics.FillRoundedRectangle(body, new Rectangle(15, 29, 34, 25), 5);
            using (Brush keyhole = new SolidBrush(Color.FromArgb(20, 184, 166)))
            {
                graphics.FillEllipse(keyhole, 29, 36, 7, 7);
                graphics.FillRectangle(keyhole, 31, 40, 3, 8);
            }

            IntPtr handle = bitmap.GetHicon();
            try
            {
                using (Icon icon = Icon.FromHandle(handle))
                using (FileStream output = File.Create(args[0]))
                    icon.Save(output);
            }
            finally
            {
                DestroyIcon(handle);
            }
        }

        return 0;
    }

    private static void FillRoundedRectangle(this Graphics graphics, Brush brush, Rectangle bounds, int radius)
    {
        int diameter = radius * 2;
        using (GraphicsPath path = new GraphicsPath())
        {
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            graphics.FillPath(brush, path);
        }
    }
}
