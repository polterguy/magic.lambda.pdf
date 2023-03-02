/*
 * Magic Cloud, copyright Aista, Ltd. See the attached LICENSE file for details.
 */

using System.IO;
using System.Text;
using iText.Kernel.Pdf;
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
        private static int _numberOfCharsToKeep = 15;
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
            var builder = new StringBuilder();
            using (var inputFile = File.OpenRead(filename))
            {
                using (var reader = new PdfReader(inputFile))
                {
                    using (var doc = new PdfDocument(reader))
                    {
                        int totalLen = 68;
                        float charUnit = ((float)totalLen) / (float)doc.GetNumberOfPages();
                        int totalWritten = 0;
                        float curUnit = 0;

                        for (int page = 1; page <= doc.GetNumberOfPages(); page++)
                        {
                            builder.Append(ExtractTextFromPDFBytes(doc.GetPage(page).GetContentBytes()) + "\r\n\r\n");

                            if (charUnit >= 1.0f)
                            {
                                for (int i = 0; i < (int)charUnit; i++)
                                {
                                    totalWritten++;
                                }
                            }
                            else
                            {
                                curUnit += charUnit;
                                if (curUnit >= 1.0f)
                                {
                                    for (int i = 0; i < (int)curUnit; i++)
                                    {
                                        totalWritten++;
                                    }
                                    curUnit = 0;
                                }
                            }
                        }
                    }
                }

            }
            return builder.ToString();
        }

        bool CheckToken(string[] tokens, char[] recent)
        {
            foreach (string token in tokens)
            {
                if ((recent[_numberOfCharsToKeep - 3] == token[0]) &&
                    (token.Length == 2 && recent[_numberOfCharsToKeep - 2] == token[1]) &&
                    ((recent[_numberOfCharsToKeep - 1] == ' ') ||
                    (recent[_numberOfCharsToKeep - 1] == 0x0d) ||
                    (recent[_numberOfCharsToKeep - 1] == 0x0a)) &&
                    ((recent[_numberOfCharsToKeep - 4] == ' ') ||
                    (recent[_numberOfCharsToKeep - 4] == 0x0d) ||
                    (recent[_numberOfCharsToKeep - 4] == 0x0a)))
                    return true;
            }
            return false;
        }

        string ExtractTextFromPDFBytes(byte[] input)
        {
            if (input == null || input.Length == 0)
                return "";

            var result = new StringBuilder();
            var inTextObject = false;
            var nextLiteral = false;
            var bracketDepth = 0;
            var previousCharacters = new char[_numberOfCharsToKeep];
            for (int j = 0; j < _numberOfCharsToKeep; j++)
            {
                previousCharacters[j] = ' ';
            }

            for (int i = 0; i < input.Length; i++)
            {
                char c = (char)input[i];
                if (inTextObject)
                {
                    if (bracketDepth == 0)
                    {
                        if (CheckToken(new string[] { "TD", "Td" }, previousCharacters))
                        {
                            result.Append("\r\n");
                        }
                        else if (CheckToken(new string[] { "'", "T*", "\"" }, previousCharacters))
                        {
                            result.Append("\r\n");
                        }
                        else if (CheckToken(new string[] { "Tj" }, previousCharacters))
                        {
                            result.Append(" ");
                        }
                    }

                    if (bracketDepth == 0 && CheckToken(new string[] { "ET" }, previousCharacters))
                    {
                        inTextObject = false;
                        result.Append("\r\n");
                    }
                    else
                    {
                        // Start outputting text
                        if ((c == '(') && (bracketDepth == 0) && (!nextLiteral))
                        {
                            bracketDepth = 1;
                        }
                        else
                        {
                            // Stop outputting text
                            if ((c == ')') && (bracketDepth == 1) && (!nextLiteral))
                            {
                                bracketDepth = 0;
                            }
                            else if (bracketDepth == 1)
                            {
                                if (c == '\\' && !nextLiteral)
                                {
                                    nextLiteral = true;
                                }
                                else
                                {
                                    if (((c >= ' ') && (c <= '~')) ||
                                        ((c >= 128) && (c < 255)))
                                        result.Append(c.ToString());
                                    nextLiteral = false;
                                }
                            }
                        }
                    }
                }

                for (int j = 0; j < _numberOfCharsToKeep - 1; j++)
                {
                    previousCharacters[j] = previousCharacters[j + 1];
                }
                previousCharacters[_numberOfCharsToKeep - 1] = c;

                if (!inTextObject && CheckToken(new string[] { "BT" }, previousCharacters))
                    inTextObject = true;
            }
            return result.ToString();
        }

        #endregion

    }
}
