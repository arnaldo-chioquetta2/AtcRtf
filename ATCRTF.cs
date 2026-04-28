using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using TeleBonifacio.gen;

// 1.3.2 Obtém ou define o conteúdo em formato RTF do controle interno.
// 1.3.1 Impedir o cursos do mouse ficar mudando a cada instante
// 1.3.0 Impressão completa em vez de ser só a primeira página
// 1.2.9 SalvaRTF publico
// 1.2.8 Caso não exista arquivo, cria
// 1.2.7 Previsão pra arquivo inicialmente inválido
// 1.2.6 Log no RTF
// 1.2.5 Campo para definir o tamanho da fonte por numero
// 1.2.4 Opção para desabilitar o auto-ajuste da fonte
// 1.2.3 Ajuste fino na impressão
// 1.2.1 Ajuste na impressão
// 1.2.0 Impressão

namespace AtcCtrl
{
    public partial class ATCRTF : UserControl
    {
        public bool Criptografia = false;
        public string caminhoDoArquivo = "";
        private bool carregando = false;

        private float vlrPerImr = 1.0f;
        private int checkPrint = 0;

        public float VlrPerImr
        {
            get { return vlrPerImr; }
            set
            {
                if (vlrPerImr != value)
                {
                    vlrPerImr = value;
                }
            }
        }

        public bool AltImprHab = true;
        private string NomeArq;

        public event EventHandler<bool> VlrPerImrChanged;

        #region SendMessage

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        // 🔥 CONSTANTES QUE FALTAVAM
        private const int WM_USER = 0x0400;
        private const int EM_FORMATRANGE = WM_USER + 57;

        [StructLayout(LayoutKind.Sequential)]
        public struct CHARRANGE
        {
            public int cpMin;
            public int cpMax;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct FORMATRANGE
        {
            public IntPtr hdc;
            public IntPtr hdcTarget;
            public RECT rc;
            public RECT rcPage;
            public CHARRANGE chrg;
        }

        #endregion

        // 🔥 LIMPEZA DO CACHE DE IMPRESSÃO (OBRIGATÓRIO)
        private void PrintDoc_EndPrint(object sender, PrintEventArgs e)
        {
            SendMessage(rtfTexto.Handle, EM_FORMATRANGE, IntPtr.Zero, IntPtr.Zero);
        }

        public ATCRTF()
        {
            InitializeComponent();
        }

        #region rtfTexto        

        private void rtfTexto_TextChanged(object sender, EventArgs e)
        {
            if (!carregando)
            {
                if (!timer1.Enabled)
                {
                    timer1.Enabled = true;
                    loga("rtfTexto_TextChanged");
                }
            }
        }

        private void rtfTexto_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Tab)
            {
                loga("rtfTexto_KeyDown Tab");
                e.Handled = true;
                e.SuppressKeyPress = true;
                int espacosParaTab = 4;
                rtfTexto.SelectedText = new string(' ', espacosParaTab);
            }
            else if (e.Control && e.KeyCode == Keys.F)
            {
                loga("rtfTexto_KeyDown Ctrl+F");
                e.SuppressKeyPress = true; 
                IniciarBusca();
            }
            else
            {
                loga("rtfTexto_KeyDown " + e.KeyCode.ToString());
            }
        }

        private string termoBusca = "";
        private int posicaoBusca = 0;
        private Cursor cursorAtual = Cursors.IBeam;

        private void IniciarBusca()
        {
            if (rtfTexto.SelectionLength > 0)
            {
                termoBusca = rtfTexto.SelectedText;
            }
            else
            {
                termoBusca = PromptBuscaTexto();
            }

            if (!string.IsNullOrEmpty(termoBusca))
            {
                posicaoBusca = rtfTexto.SelectionStart + rtfTexto.SelectionLength; // Começa a busca após a seleção
                BuscarTexto();
            }
        }

        private string PromptBuscaTexto()
        {
            Form prompt = new Form()
            {
                Width = 300,
                Height = 150,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = "Buscar",
                StartPosition = FormStartPosition.CenterScreen
            };

            Label lbl = new Label() { Left = 10, Top = 20, Text = "Buscar:", AutoSize = true };
            TextBox inputBox = new TextBox() { Left = 70, Top = 18, Width = 200 };
            Button confirmButton = new Button() { Text = "Ok", Left = 110, Width = 80, Top = 50, DialogResult = DialogResult.OK };

            confirmButton.Click += (sender, e) => { prompt.Close(); };
            prompt.Controls.Add(lbl);
            prompt.Controls.Add(inputBox);
            prompt.Controls.Add(confirmButton);
            prompt.AcceptButton = confirmButton;

            return prompt.ShowDialog() == DialogResult.OK ? inputBox.Text : "";
        }


        private void BuscarTexto()
        {
            if (string.IsNullOrEmpty(termoBusca))
                return;

            int novaPosicao = rtfTexto.Find(termoBusca, posicaoBusca, RichTextBoxFinds.None);

            if (novaPosicao >= 0)
            {
                rtfTexto.Select(novaPosicao, termoBusca.Length);
                rtfTexto.ScrollToCaret();
                posicaoBusca = novaPosicao + termoBusca.Length; // Avança para a próxima ocorrência
            }
            else
            {
                MessageBox.Show("Texto não encontrado.", "Busca", MessageBoxButtons.OK, MessageBoxIcon.Information);
                posicaoBusca = 0; // Reinicia a busca do início
            }
        }



        #endregion

        private void timer1_Tick(object sender, EventArgs e)
        {
            timer1.Enabled = false;
            SalvaRTF();
            if (timer1.Interval > 500)
            {
                timer1.Interval -= 100;
            }
        }

        #region Arquivo        

        public void Carrega()
        {
            string Texto = "";
            try
            {
                if (string.IsNullOrEmpty(caminhoDoArquivo))
                    throw new ArgumentException("Caminho do arquivo não definido.");
                if (!File.Exists(caminhoDoArquivo))
                {
                    Texto = @"{\rtf1\ansi\deff0{\fonttbl{\f0 Arial;}}\viewkind4\uc1\pard\f0\fs20 \par}";
                    File.WriteAllText(caminhoDoArquivo, Texto);
                }
                else
                {
                    Texto = File.ReadAllText(caminhoDoArquivo);
                    if (string.IsNullOrWhiteSpace(Texto))
                    {
                        Texto = @"{\rtf1\ansi\deff0{\fonttbl{\f0 Arial;}}\viewkind4\uc1\pard\f0\fs20 \par}";
                        File.WriteAllText(caminhoDoArquivo, Texto);
                    }
                }
            }
            catch (Exception)
            {
                //glo.Loga($"Erro ao carregar arquivo RTF: {caminhoDoArquivo} - {ex.Message}");
                return;
            }

            this.NomeArq = Path.GetFileNameWithoutExtension(caminhoDoArquivo);

            if (Criptografia)
            {
                try
                {
                    Texto = Cripto.Decrypt(Texto);
                }
                catch
                {
                    // Se falhar a descriptografia, tenta ler texto puro
                }
            }

            try
            {
                rtfTexto.Rtf = Texto;
            }
            catch
            {
                try
                {
                    Texto = @"{\rtf1\ansi\deff0{\fonttbl{\f0 Arial;}}\viewkind4\uc1\pard\f0\fs20 \par}";
                    File.WriteAllText(caminhoDoArquivo, Texto);
                    rtfTexto.Rtf = Texto;
                }
                catch (Exception)
                {
                    //glo.Loga($"Falha ao autocorrigir RTF: {caminhoDoArquivo} - {ex.Message}");
                }
            }
        }


        public void SalvaRTF()
        {
            string Texto = rtfTexto.Rtf;
            if (Criptografia)
            {
                Texto = Cripto.Encrypt(Texto);
            }
            File.WriteAllText(caminhoDoArquivo, Texto);
        }

        #endregion

        #region Botões 

        private void ApplyStyleToLineOrSelection(Action<Font> fontAction = null, Action colorAction = null)
        {
            int selectionStart = rtfTexto.SelectionStart;
            int selectionLength = rtfTexto.SelectionLength;

            if (selectionLength == 0)
            {
                // Não há seleção, então selecione a linha inteira
                SelectCurrentLine();
            }

            if (fontAction != null && rtfTexto.SelectionFont != null)
            {
                Font currentFont = rtfTexto.SelectionFont;
                fontAction(currentFont);
            }

            if (colorAction != null)
            {
                colorAction();
            }

            // Restaure a seleção original ou a posição do cursor
            rtfTexto.Select(selectionStart, selectionLength);
        }

        private void SelectCurrentLine()
        {
            int lineStart = rtfTexto.GetFirstCharIndexOfCurrentLine();
            int lineEnd = rtfTexto.Text.IndexOf('\n', lineStart);
            if (lineEnd == -1) lineEnd = rtfTexto.Text.Length;
            rtfTexto.Select(lineStart, lineEnd - lineStart);
        }

        private void toolStripButtonRedo_Click(object sender, EventArgs e)
        {
            if (rtfTexto.CanRedo)
            {
                rtfTexto.Redo();
            }
        }

        private void toolStripButtonUndo_Click(object sender, EventArgs e)
        {
            if (rtfTexto.CanUndo)
            {
                rtfTexto.Undo();
            }
        }

        private void toolStripButtonUnderline_Click(object sender, EventArgs e)
        {
            ApplyStyleToLineOrSelection(currentFont =>
            {
                FontStyle newStyle = currentFont.Style ^ FontStyle.Underline;
                rtfTexto.SelectionFont = new Font(currentFont.FontFamily, currentFont.Size, newStyle);
            });
        }

        private void toolStripButtonItalic_Click(object sender, EventArgs e)
        {
            ApplyStyleToLineOrSelection(currentFont =>
            {
                FontStyle newStyle = currentFont.Style ^ FontStyle.Italic;
                rtfTexto.SelectionFont = new Font(currentFont.FontFamily, currentFont.Size, newStyle);
            });
        }

        private void toolStripButtonBold_Click(object sender, EventArgs e)
        {
            ApplyStyleToLineOrSelection(currentFont =>
            {
                FontStyle newStyle = currentFont.Style ^ FontStyle.Bold;
                rtfTexto.SelectionFont = new Font(currentFont.FontFamily, currentFont.Size, newStyle);
            });
        }

        private void tsVermelho_Click(object sender, EventArgs e)
        {
            ApplyStyleToLineOrSelection(null, () => rtfTexto.SelectionColor = Color.Red);
        }

        private void tsAzul_Click(object sender, EventArgs e)
        {
            ApplyStyleToLineOrSelection(null, () => rtfTexto.SelectionColor = Color.Blue);
        }

        private void tsVerde_Click(object sender, EventArgs e)
        {
            ApplyStyleToLineOrSelection(null, () => rtfTexto.SelectionColor = Color.Green);
        }

        private void tsLaranja_Click(object sender, EventArgs e)
        {
            ApplyStyleToLineOrSelection(null, () => rtfTexto.SelectionColor = Color.Orange);
        }

        private void tsPreto_Click(object sender, EventArgs e)
        {
            ApplyStyleToLineOrSelection(null, () => rtfTexto.SelectionColor = Color.Black);
        }

        private void tsCinza_Click(object sender, EventArgs e)
        {
            ApplyStyleToLineOrSelection(null, () => rtfTexto.SelectionColor = Color.Gray);
        }

        private void toolStripButtonEncrypt_Click(object sender, EventArgs e)
        {
            //tsDescriptar.Visible = true;
            //tsEncriptar.Visible = false;
            //Criptografia = true;
            //SalvaRTF();
        }

        private void tsDescriptar_Click(object sender, EventArgs e)
        {
            //tsDescriptar.Visible = false;
            //tsEncriptar.Visible = true;
            //Criptografia = false;
            //SalvaRTF();
        }

        public void txPercVisivel(bool Ativo)
        {
            if (imageList1 != null && imageList1.Images != null)
            {
                if (Ativo && imageList1.Images.ContainsKey("toggle_on"))
                {
                    tsToggle.Image = imageList1.Images["toggle_on"];
                    tsToggle.Checked = true;
                }
                else if (imageList1.Images.ContainsKey("toggle_off"))
                {
                    tsToggle.Image = imageList1.Images["toggle_off"];
                    tsToggle.Checked = false;
                }
            }
            txPerc.Visible = true;
            tsToggle.Visible = true;
            lbAutoAjuste.Visible = true;
            lbFator.Visible = true;
            txPerc.Enabled = Ativo;
            this.AltImprHab = Ativo;
        }

        //public void txPercVisivel(bool Ativo)
        //{
        //    txPerc.Visible = true;
        //    tsToggle.Visible = true;
        //    lbAutoAjuste.Visible = true;
        //    lbFator.Visible = true;
        //    if (Ativo)
        //    {
        //        tsToggle.Image = imageList1.Images["toggle_on"];
        //        tsToggle.Checked = true;
        //    } else
        //    {
        //        tsToggle.Image = imageList1.Images["toggle_off"];
        //        tsToggle.Checked = false;
        //    }
        //    txPerc.Enabled = Ativo;
        //    this.AltImprHab = Ativo;
        //}

        private void txPerc_KeyUp(object sender, KeyEventArgs e)
        {
            this.VlrPerImr = LeValor(txPerc.Text);
        }

        private float LeValor(string text)
        {
            string valorLimpo = new string(text.Where(c => char.IsDigit(c) || c == ',' || c == '.').ToArray());
            char decimalSeparator = CultureInfo.CurrentCulture.NumberFormat.CurrencyDecimalSeparator[0];
            if (valorLimpo.Contains('.') && valorLimpo.Contains(','))
            {
                valorLimpo = valorLimpo.Replace(".", decimalSeparator.ToString());
            }
            else if (valorLimpo.Contains('.') || valorLimpo.Contains(','))
            {
                valorLimpo = valorLimpo.Replace(',', decimalSeparator).Replace('.', decimalSeparator);
            }
            if (float.TryParse(valorLimpo, out float valorFloat))
            {
                return valorFloat;
            }
            else
            {
                return 0.0f;
            }
        }

        #endregion

        #region URL        

        private bool IsValidUrl(string text)
        {
            string pattern = @"^(https?:\/\/)?([\da-z\.-]+)\.([a-z\.]{2,6})([\/\w \.-]*)*\/?$";
            return Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase);
        }

        private string GetWordAtIndex(int index)
        {
            string text = rtfTexto.Text;
            int start = index;
            int end = index;

            while (start > 0 && !char.IsWhiteSpace(text[start - 1]))
            {
                start--;
            }

            while (end < text.Length && !char.IsWhiteSpace(text[end]))
            {
                end++;
            }

            return text.Substring(start, end - start);
        }

        private void rtfTexto_MouseMove(object sender, MouseEventArgs e)
        {
            int charIndex = rtfTexto.GetCharIndexFromPosition(e.Location);

            Cursor novoCursor = Cursors.IBeam;

            if (charIndex >= 0 && charIndex < rtfTexto.Text.Length)
            {
                string word = GetWordAtIndex(charIndex);

                if (IsValidUrl(word))
                {
                    novoCursor = Cursors.Hand;
                }
            }

            // 🔥 SÓ ALTERA SE FOR DIFERENTE
            if (cursorAtual != novoCursor)
            {
                cursorAtual = novoCursor;
                rtfTexto.Cursor = novoCursor;
            }
        }

        private void rtfTexto_LinkClicked(object sender, LinkClickedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.LinkText) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao abrir o link: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        #region Impressão        

        private void btImpr_Click(object sender, EventArgs e)
        {
            if (tsToggle.Visible)
            {
                if (this.AltImprHab)
                {
                    AjustarFonteParaTamanhoMaximo();
                }                
            }            
            PrintDocument printDoc = new PrintDocument();
            printDoc.DefaultPageSettings.Landscape = true; // Define para modo paisagem
            printDoc.PrintPage += new PrintPageEventHandler(PrintDoc_PrintPage);
            PrintPreviewDialog printPreview = new PrintPreviewDialog();
            printPreview.Document = printDoc;
            printPreview.Width = 800;
            printPreview.Height = 600;
            printPreview.ShowDialog();
        }

        private void PrintDoc_PrintPage(object sender, PrintPageEventArgs e)
        {
            checkPrint = rtfTexto.PrintRTFContent(e, checkPrint);
                
            if (checkPrint < rtfTexto.TextLength)
            {
                e.HasMorePages = true;
            }
            else
            {
                e.HasMorePages = false;
                checkPrint = 0;
            }
        }

        private void AjustarFonteParaTamanhoMaximo()
        {
            using (Graphics g = this.CreateGraphics())
            {
                int larguraPagina = (int)(new PrintDocument().DefaultPageSettings.PaperSize.Width * 0.9); // Considera uma margem de 10%
                string[] linhas = rtfTexto.Lines;
                string linhaMaisLonga = linhas.OrderByDescending(l => l.Length).FirstOrDefault();
                if (linhaMaisLonga == null) return; 
                int tamanhoFonte = 10; // Começa com uma fonte razoável
                Font fonteAjustada;
                do
                {
                    fonteAjustada = new Font(rtfTexto.Font.FontFamily, tamanhoFonte, rtfTexto.Font.Style);
                    SizeF tamanhoTexto = g.MeasureString(linhaMaisLonga, fonteAjustada);
                    if (tamanhoTexto.Width > larguraPagina)
                    {
                        tamanhoFonte--; 
                        break;
                    }
                    tamanhoFonte++; 
                }
                while (true);
                if (this.VlrPerImr != 1.0f)
                {
                    tamanhoFonte = (int)(tamanhoFonte * this.VlrPerImr);
                }
                VlrPerImrChanged?.Invoke(this, tsToggle.Checked);
                rtfTexto.Font = new Font(rtfTexto.Font.FontFamily, tamanhoFonte, rtfTexto.Font.Style);
            }
        }

        private void tsToggle_CheckedChanged(object sender, EventArgs e)
        {
            if (tsToggle.Checked)
            {
                tsToggle.Image = imageList1.Images["toggle_on"];
                txPerc.Text = "1 ";
            }
            else
            {
                tsToggle.Image = imageList1.Images["toggle_off"];
            }
            txPerc.Enabled = tsToggle.Checked;
            this.AltImprHab = tsToggle.Checked;
        }


        #endregion

        #region Fonte

        private void toolStripButtonDecreaseFont_Click(object sender, EventArgs e)
        {
            loga("DecreaseFont");
            ApplyStyleToLineOrSelection(currentFont =>
            {
                float newSize = Math.Max(currentFont.Size - 1, 1); // Garante que o tamanho mínimo seja 1
                rtfTexto.SelectionFont = new Font(currentFont.FontFamily, newSize, currentFont.Style);
            });
        }

        private void toolStripButtonIncreaseFont_Click(object sender, EventArgs e)
        {
            loga("IncreaseFont");
            ApplyStyleToLineOrSelection(currentFont =>
            {
                float newSize = currentFont.Size + 1;
                rtfTexto.SelectionFont = new Font(currentFont.FontFamily, newSize, currentFont.Style);
            });
        }

        private void rtfTexto_SelectionChanged(object sender, EventArgs e)
        {
            if (rtfTexto.SelectionFont != null)
            {
                float fontSize = rtfTexto.SelectionFont.Size;
                txTamFonte.Text = fontSize.ToString(CultureInfo.InvariantCulture); 
            }
            else
            {
                txTamFonte.Text = ""; 
            }
        }

        private void txTamFonte_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) // Aplique o tamanho quando pressionar Enter
            {
                AlterarTamanhoFonteSelecionada();
            }
        }

        private void AlterarTamanhoFonteSelecionada()
        {
            loga("AlterarTamanhoFonteSelecionada");
            if (rtfTexto.SelectionLength > 0 )
            {
                string sTam = txTamFonte.Text.Replace(",", ".");
                loga("sTam = "+ sTam);
                if (float.TryParse(sTam, NumberStyles.Float, CultureInfo.InvariantCulture, out float novoTamanho))
                {
                    Font currentFont = rtfTexto.SelectionFont;                    
                    if (currentFont != null)
                    {
                        rtfTexto.SelectionFont = new Font(currentFont.FontFamily, novoTamanho, currentFont.Style);
                        loga("Setou rtfTexto.SelectionFont");
                    } else
                    {
                        loga("currentFont = null");
                    }
                } else
                {
                    loga("Erro no Tray");
                }
            }
            else
            {
                loga("Selecione texto no editor para alterar o tamanho da fonte.");
                MessageBox.Show("Selecione texto no editor para alterar o tamanho da fonte.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void loga(string message)
        {            
            string logFilePath = @"C:\Entregas\rtf.txt";
            using (StreamWriter writer = new StreamWriter(logFilePath, true))
            {
                string Texto = $"{DateTime.Now} {this.NomeArq} {message}";
                writer.WriteLine(Texto);
                Console.WriteLine(Texto);
            }            
        }

        #endregion

        public List<TextoFormatado> ObterTextoFormatado()
        {
            List<TextoFormatado> resultado = new List<TextoFormatado>();
            rtfTexto.SelectAll();

            int length = rtfTexto.TextLength;
            int start = 0;

            while (start < length)
            {
                rtfTexto.Select(start, 1);

                var fonte = rtfTexto.SelectionFont;
                var cor = rtfTexto.SelectionColor;

                int runLength = 1;

                while (start + runLength < length)
                {
                    rtfTexto.Select(start + runLength, 1);
                    if (!rtfTexto.SelectionFont.Equals(fonte) || rtfTexto.SelectionColor != cor)
                        break;
                    runLength++;
                }

                rtfTexto.Select(start, runLength);
                string texto = rtfTexto.SelectedText;

                resultado.Add(new TextoFormatado
                {
                    Texto = texto,
                    Estilo = fonte.Style,
                    Fonte = fonte.FontFamily.Name,
                    Tamanho = fonte.Size,
                    Cor = cor
                });

                start += runLength;
            }

            return resultado;
        }

        [Browsable(true)]
        [Category("Appearance")]
        [Description("Obtém ou define o conteúdo em formato RTF do controle interno.")]
        public string RtfConteudo
        {
            get
            {
                return rtfTexto.Rtf;
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    rtfTexto.Clear();
                else
                    rtfTexto.Rtf = value;
            }
        }

    }

    public class TextoFormatado
    {
        public string Texto { get; set; }
        public FontStyle Estilo { get; set; }
        public float Tamanho { get; set; }
        public string Fonte { get; set; }
        public Color Cor { get; set; }
    }


}
