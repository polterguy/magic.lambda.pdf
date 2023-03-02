/*
 * Magic Cloud, copyright Aista, Ltd. See the attached LICENSE file for details.
 */

using System.IO;
using System.Text;
using System.Collections.Generic;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using magic.node;
using magic.node.contracts;
using magic.node.extensions;
using magic.signals.contracts;

namespace magic.lambda.pdf
{
    /// <summary>
    /// [pdf2text] slot for retrieving a configuration key.
    /// </summary>
    [Slot(Name = "pdf2text")]
    public class Pdf2Text : ISlot
    {
        readonly IRootResolver _rootResolver;

        public Pdf2Text(IRootResolver rootResolver)
        {
            _rootResolver = rootResolver;
        }

        /// <summary>
        /// Implementation of your slot.
        /// </summary>
        /// <param name="signaler">Signaler used to signal your slot.</param>
        /// <param name="input">Arguments to your slot.</param>
        public void Signal(ISignaler signaler, Node input)
        {
            input.Value = ExtractText(_rootResolver.AbsolutePath(input.GetEx<string>()));
        }

        #region [ -- Private helper methods -- ]

        string ExtractText(string filename)
        {
            using (PdfReader reader = new PdfReader(filename))
            {
                var builder = new StringBuilder();

                using (var doc = new PdfDocument(reader))
                {
                    for (int idx = 1; idx <= doc.GetNumberOfPages(); idx++)
                    {
                        var content = PdfTextExtractor.GetTextFromPage(doc.GetPage(idx));
                        string[] lines = content.Split('\n');
                        foreach (var idxLine in lines)
                        {
                            builder.Append(idxLine.Trim()).Append("\r\n");
                        }
                        if (lines.Length > 0)
                            builder.Append("\r\n");
                    }
                }
                return builder.ToString().Trim();
            }
        }


        #endregion

    }
}
