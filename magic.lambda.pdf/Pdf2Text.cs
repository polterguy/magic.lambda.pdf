/*
 * Magic Cloud, copyright Aista, Ltd. See the attached LICENSE file for details.
 */

using System.IO;
using System.Text;
using iText.Kernel.Pdf;
using magic.node;
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

        /// <summary>
        /// Implementation of your slot.
        /// </summary>
        /// <param name="signaler">Signaler used to signal your slot.</param>
        /// <param name="input">Arguments to your slot.</param>
        public void Signal(ISignaler signaler, Node input)
        {
            input.Value = ExtractText(input.GetEx<byte[]>());
        }

        #region [ -- Private helper methods -- ]

        string ExtractText(byte[] bytes)
        {
            var builder = new StringBuilder();
            using (var memStream = new MemoryStream())
            {
                memStream.Write(bytes);
                memStream.Position = 0;
                var reader = new PdfReader(memStream);
                var doc = new PdfDocument(reader);

                int totalLen = 68;
                float charUnit = ((float)totalLen) / (float)doc.GetNumberOfPages();
                int totalWritten = 0;
                float curUnit = 0;

                for (int page = 1; page <= doc.GetNumberOfPages(); page++)
                {
                    builder.Append(ExtractTextFromPDFBytes(doc.GetPage(page).GetContentBytes()) + " ");

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
            return builder.ToString();
        }

        bool CheckToken(string[] tokens, char[] recent)
        {
            foreach (string token in tokens)
            {
                if ((recent[_numberOfCharsToKeep - 3] == token[0]) &&
                    (recent[_numberOfCharsToKeep - 2] == token[1]) &&
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

            string resultString = "";
            bool inTextObject = false;
            bool nextLiteral = false;
            int bracketDepth = 0;
            char[] previousCharacters = new char[_numberOfCharsToKeep];
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
                        resultString += "\n\r";
                        }
                        else
                        {
                            if (CheckToken(new string[] { "'", "T*", "\"" }, previousCharacters))
                            {
                                resultString += "\n";
                            }
                            else
                            {
                                if (CheckToken(new string[] { "Tj" }, previousCharacters))
                                {
                                    resultString += " ";
                                }
                            }
                        }
                    }

                    // End of a text object, also go to a new line.
                    if (bracketDepth == 0 && CheckToken(new string[] { "ET" }, previousCharacters))
                    {
                        inTextObject = false;
                        resultString += " ";
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
                            else
                            {
                                // Just a normal text character:
                                if (bracketDepth == 1)
                                {
                                    // Only print out next character no matter what. 
                                    // Do not interpret.
                                    if (c == '\\' && !nextLiteral)
                                    {
                                        nextLiteral = true;
                                    }
                                    else
                                    {
                                        if (((c >= ' ') && (c <= '~')) ||
                                            ((c >= 128) && (c < 255)))
                                            resultString += c.ToString();
                                        nextLiteral = false;
                                    }
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
            return resultString;
        }

        #endregion
    }
}
