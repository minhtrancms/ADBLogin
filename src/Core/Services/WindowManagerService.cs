using System;
using System.Drawing;
using System.Windows.Forms;
using OpenQA.Selenium;

namespace ADBLogin.Core.Services
{
    /// <summary>
    /// Service tu dong sap xep va chia luoi cua so trinh duyet (Tile / Grid Arrangement)
    /// </summary>
    public class WindowManagerService
    {
        public int DefaultRows { get; set; }
        public int DefaultColumns { get; set; }

        public WindowManagerService(int defaultRows = 2, int defaultColumns = 4)
        {
            DefaultRows = defaultRows > 0 ? defaultRows : 2;
            DefaultColumns = defaultColumns > 0 ? defaultColumns : 4;
        }

        /// <summary>
        /// Tinh toan toa do vi tri va kich thuoc cho cua so trinh duyet dua tren chi so (index)
        /// </summary>
        public Rectangle CalculateWindowBounds(int index, int rows = 0, int columns = 0)
        {
            int r = rows > 0 ? rows : DefaultRows;
            int c = columns > 0 ? columns : DefaultColumns;

            var screen = Screen.PrimaryScreen.WorkingArea;
            int windowWidth = screen.Width / c;
            int windowHeight = screen.Height / r;

            int colIndex = index % c;
            int rowIndex = (index / c) % r;

            int x = screen.Left + (colIndex * windowWidth);
            int y = screen.Top + (rowIndex * windowHeight);

            return new Rectangle(x, y, windowWidth, windowHeight);
        }

        /// <summary>
        /// Ap dung vi tri va kich thuoc truc tiep vao IWebDriver
        /// </summary>
        public void ApplyWindowBounds(IWebDriver driver, int index, int rows = 0, int columns = 0)
        {
            if (driver == null) return;

            try
            {
                var bounds = CalculateWindowBounds(index, rows, columns);
                driver.Manage().Window.Position = new Point(bounds.X, bounds.Y);
                driver.Manage().Window.Size = new Size(bounds.Width, bounds.Height);
            }
            catch (Exception)
            {
                // Bo qua neu trinh duyet khong ho tro hoac bi dong bat ngo
            }
        }
    }
}
