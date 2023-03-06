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
    /// [pdf2text] slot for retrieving text content from a PDF file or stream.
    /// </summary>
    [Slot(Name = "pdf2text")]
    public class Pdf2Text : ISlot
    {
        readonly IRootResolver _rootResolver;

        /// <summary>
        /// Creates an instance of your type
        /// </summary>
        /// <param name="rootResolver">Needed to resolve root folder of cloudlet</param>
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
            var value = input.GetEx<object>();
            if (value is string filename)
                input.Value = ExtractFromFile(_rootResolver.AbsolutePath(filename));
            else if (value is Stream stream)
                input.Value = ExtractFromStream(stream);
            else if (value is byte[] bytes)
                input.Value = ExtractFromBytes(bytes);
        }

        #region [ -- Private helper methods -- ]

        /*
         * Reads PDF file from the specified (absolute) filename.
         */
        string ExtractFromFile(string filename)
        {
            using (var reader = new PdfReader(filename))
            {
                return Extract(reader);
            }
        }

        /*
         * Reads PDF file from the specified stream.
         */
        string ExtractFromStream(Stream stream)
        {
            using (var reader = new PdfReader(stream))
            {
                return Extract(reader);
            }
        }

        /*
         * Reads PDF file from the specified bytes.
         */
        string ExtractFromBytes(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                using (var reader = new PdfReader(stream))
                {
                    return Extract(reader);
                }
            }
        }

        /*
         * Extracts all text from the specified PdfReader.
         */
        string Extract(PdfReader reader)
        {
            var builder = new StringBuilder();
            using (var doc = new PdfDocument(reader))
            {
                for (var idx = 1; idx <= doc.GetNumberOfPages(); idx++)
                {
                    var content = PdfTextExtractor.GetTextFromPage(doc.GetPage(idx));
                    var lines = content.Split('\n');
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

        #endregion
    }
}
