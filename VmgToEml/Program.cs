/*
 * The MIT License
 *
 * Copyright 2020 Palmtree Software.
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;

namespace VmgToEml
{

    // see also http://creation.mb.softbank.jp/mc/tech/doc/A-007-111-Media_2.1.0(MC).pdf


    class Program
    {

        static void Main(string[] args)
        {
            var dir = new DirectoryInfo(".");
            foreach (var inFile in dir.EnumerateFiles("*.vmg"))
            {
                using (var inStream = inFile.OpenRead())
                using (var inBuffer = new BufferedStream(inStream))
                {
                    var reader = new InputFileBuffer(inBuffer);
                    while (!reader.IsEof())
                    {
                        TranslateVMSG(reader, inFile.Directory);
                    }
                }
            }

        }

        private static void TranslateVMSG(InputFileBuffer reader, DirectoryInfo outFileDir)
        {
            if (!reader.StartsWith("BEGIN:VMSG"))
                throw new Exception(string.Format("BEGIN:VMSGが見つかりません。: pos={0}", reader.Position));
            reader.Drop(10);
            reader.DropNewLine();
            while (!reader.IsEof())
            {
                if (reader.StartsWith("END:VMSG"))
                    break;
                else if (reader.StartsWith("BEGIN:VENV"))
                    TranslateVENV(reader, outFileDir);
                else if (reader.StartsWith("BEGIN:VCARD"))
                    TranslateVCARD(reader);
                else
                {
                    var data = reader.ReadLine();
                    //System.Diagnostics.Debug.WriteLine("skipped: " + Encoding.ASCII.GetString(data));
                }
            }
            if (!reader.StartsWith("END:VMSG"))
                throw new Exception(string.Format("END:VMSGが見つかりません。: pos={0}", reader.Position));
            reader.Drop(8);
            reader.DropNewLine();
        }

        private static void TranslateVENV(InputFileBuffer reader, DirectoryInfo outFileDir)
        {
            if (!reader.StartsWith("BEGIN:VENV"))
                throw new Exception(string.Format("BEGIN:VENVが見つかりません。: pos={0}", reader.Position));
            reader.Drop(10);
            reader.DropNewLine();
            if (reader.StartsWith("BEGIN:VENV"))
                TranslateVENV(reader, outFileDir);
            else if (reader.StartsWith("BEGIN:VCARD"))
                TranslateVCARD(reader);
            else if (reader.StartsWith("BEGIN:VBODY"))
                TranslateVBODY(reader, outFileDir);
            else
                throw new Exception(string.Format("未知のヘッダです。({1}): pos={0}", reader.Position, Encoding.ASCII.GetString(reader.ReadLine())));
            if (!reader.StartsWith("END:VENV"))
                throw new Exception(string.Format("END:VENVが見つかりません。: pos={0}", reader.Position));
            reader.Drop(8);
            reader.DropNewLine();
        }

        private static void TranslateVCARD(InputFileBuffer reader)
        {
            if (!reader.StartsWith("BEGIN:VCARD"))
                throw new Exception(string.Format("BEGIN:VCARDが見つかりません。: pos={0}", reader.Position));
            reader.Drop(11);
            reader.DropNewLine();
            while (!reader.IsEof())
            {
                if (reader.StartsWith("END:VCARD"))
                    break;
                else
                {
                    // NOP
                }
            }
            if (!reader.StartsWith("END:VCARD"))
                throw new Exception(string.Format("END:VCARDが見つかりません。: pos={0}", reader.Position));
            reader.Drop(9);
            reader.DropNewLine();
        }

        private static Regex _mailAddressPattern1 = new Regex(@"<(?<mailAddress>[a-zA-Z0-9][a-zA-Z0-9\._-]*@[a-zA-Z0-9_-][a-zA-Z0-9\._-]*)>", RegexOptions.Compiled);
        private static Regex _mailAddressPattern2 = new Regex(@"(?<mailAddress>[a-zA-Z0-9][a-zA-Z0-9\._-]*@[a-zA-Z0-9_-][a-zA-Z0-9\._-]*)", RegexOptions.Compiled);

        private static void TranslateVBODY(InputFileBuffer reader, DirectoryInfo outFileDir)
        {
            if (!reader.StartsWith("BEGIN:VBODY"))
                throw new Exception(string.Format("BEGIN:VBODYが見つかりません。: pos={0}", reader.Position));
            reader.Drop(11);
            reader.DropNewLine();
            var headers = new List<string>();
            while (!reader.IsEof())
            {
                // 最初の空行までヘッダとみなす
                var header = Encoding.ASCII.GetString(reader.ReadLine());
                if (header.Trim() == "")
                    break;
                headers.Add(header);
            }
            if (reader.IsEof())
                throw new Exception(string.Format("END:VBODYが見つかりません。: pos={0}", reader.Position));
            var fromMailAddress = headers
                .Where(s => s.StartsWith("From:"))
                .Select(s => s.Substring(5).Trim())
                .Select(s => {
                    var m = _mailAddressPattern1.Match(s);
                    if (!m.Success)
                        m = _mailAddressPattern2.Match(s);
                    if (!m.Success)
                        return null;
                    else
                        return m.Groups["mailAddress"].Value;

                })
                .Where(s => s != null)
                .FirstOrDefault();
            var date = headers
                .Where(s => s.StartsWith("Date:"))
                .Select(s => s.Substring(5).Trim())
                .Select(s => {
                    DateTime dateTime;
                    return DateTime.TryParse(s, out dateTime) ? dateTime : (DateTime?)null;
                })
                .Where(dateTime => dateTime != null)
                .FirstOrDefault();
            if (fromMailAddress == null || date == null)
                throw new Exception("FromヘッダまたはDateヘッダがありません。");
            var messageId = string.Format("{0}.{1}", date.Value.ToString("yyyyMMddHHmmss"), fromMailAddress);
            headers.Insert(0, string.Format("Message-Id: {0}\r\n", messageId));
            var outFile = new FileInfo(Path.Combine(outFileDir.FullName, messageId + ".eml"));
            using (var outStream = outFile.Create())
            {
                var writer = new OutputFileBuffer(outStream);
                // ヘッダの出力
                foreach (var header in headers)
                {
                    writer.Write(header);
                }
                // ヘッダの後の空行の出力
                writer.Write("\r\n");
                // "END:VBODY" が見つかるまでコピー
                while (!reader.IsEof())
                {
                    if (reader.StartsWith("END:VBODY"))
                        break;
                    var data = reader.ReadLine();
                    writer.Write(data);
                    //System.Diagnostics.Debug.WriteLine("copied: " + Encoding.ASCII.GetString(data));

                }
            }
            if (!reader.StartsWith("END:VBODY"))
                throw new Exception(string.Format("END:VBODYが見つかりません。: pos={0}", reader.Position));
            reader.Drop(9);
            reader.DropNewLine();
        }

    }
}
