using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AtcCtrl
{
    public class RichTextBoxPrintCtrl : RichTextBox
    {
        [DllImport("USER32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private const int WM_USER = 0x0400;
        private const int EM_FORMATRANGE = WM_USER + 57;
        private int checkPrint = 0;

        public int PrintRTFContent(PrintPageEventArgs e, int checkPrint)
        {
            // Área de impressão (margens)
            RECT rectToPrint = new RECT
            {
                Top = HundredthsInchToTwips(e.MarginBounds.Top),
                Bottom = HundredthsInchToTwips(e.MarginBounds.Bottom),
                Left = HundredthsInchToTwips(e.MarginBounds.Left),
                Right = HundredthsInchToTwips(e.MarginBounds.Right)
            };

            // Área total da página
            RECT rectPage = new RECT
            {
                Top = HundredthsInchToTwips(e.PageBounds.Top),
                Bottom = HundredthsInchToTwips(e.PageBounds.Bottom),
                Left = HundredthsInchToTwips(e.PageBounds.Left),
                Right = HundredthsInchToTwips(e.PageBounds.Right)
            };

            IntPtr hdc = e.Graphics.GetHdc();

            FORMATRANGE fmtRange = new FORMATRANGE
            {
                hdc = hdc,
                hdcTarget = hdc,
                rc = rectToPrint,
                rcPage = rectPage,

                // 🔥 AQUI ESTÁ A CORREÇÃO
                chrg = new CHARRANGE
                {
                    cpMin = checkPrint,         // começa de onde parou
                    cpMax = this.TextLength     // vai até o fim
                }
            };

            IntPtr wParam = new IntPtr(1);
            IntPtr lParam = Marshal.AllocCoTaskMem(Marshal.SizeOf(fmtRange));
            Marshal.StructureToPtr(fmtRange, lParam, false);

            // 🔥 RETORNA O PRÓXIMO CARACTERE
            int nextChar = SendMessage(this.Handle, EM_FORMATRANGE, wParam, lParam).ToInt32();

            Marshal.FreeCoTaskMem(lParam);
            e.Graphics.ReleaseHdc(hdc);

            return nextChar;
        }

        private int HundredthsInchToTwips(int n)
        {
            return (int)(n * 14.4);
        }

        protected override void Dispose(bool disposing)
        {
            // Libera a memória usada pelo formato da impressão
            IntPtr wParam = new IntPtr(0);
            SendMessage(this.Handle, EM_FORMATRANGE, wParam, IntPtr.Zero);
            base.Dispose(disposing);
        }

        // Estruturas necessárias para a impressão
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CHARRANGE
        {
            public int cpMin;
            public int cpMax;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FORMATRANGE
        {
            public IntPtr hdc;
            public IntPtr hdcTarget;
            public RECT rc;
            public RECT rcPage;
            public CHARRANGE chrg;
        }
    }

}

