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
using System.Linq;
using System.Text;
using System.IO;

namespace VmgToEml
{
    class InputFileBuffer
    {
        private Stream _stream;
        private byte[] _buffer = new byte[0];
        private bool _is_eof = false;
        private int _pos = 0;

        public InputFileBuffer(Stream stream)
        {
            _stream = stream;
        }

        public bool IsEof()
        {
            return _buffer.Length == 0 && _is_eof;
        }

        public bool StartsWith(string s, int startIndex = 0)
        {
            var data = Encoding.ASCII.GetBytes(s);
            return StartsWith(data, data.Length, startIndex);
        }

        public bool StartsWith(byte[] data, int dataLength, int startIndex = 0)
        {
            ReadData(startIndex + dataLength);
            if (_buffer.Length < startIndex + data.Length)
                return false;
            for (var index = 0; index < data.Length; ++index)
            {
                if (_buffer[startIndex + index] != data[index])
                    return false;
            }
            return true;
        }

        public int IndexOf(char x, int startIndex = 0)
        {
            if (x < byte.MinValue || x > byte.MaxValue)
                throw new Exception("内部エラーです。");
            return IndexOf((byte)x, startIndex);
        }

        public int IndexOf(byte d, int startIndex = 0)
        {
            var index = 0;
            while (true)
            {
                ReadData(startIndex + index + 1);
                if (startIndex + index + 1 > _buffer.Length)
                    return -1;
                if (_buffer[startIndex + index] == d)
                    return index;
                ++index;
            }
        }

        public int IndexAnyOf(char[] data, int startIndex = 0)
        {
            return IndexAnyOf(data.Select(c =>
            {
                if (c < byte.MinValue || c > byte.MaxValue)
                    throw new Exception("内部エラーです。");
                return (byte)c;
            }).ToArray(), startIndex);
        }

        public int IndexAnyOf(byte[] data, int startIndex = 0)
        {
            var index = 0;
            while (true)
            {
                ReadData(startIndex + index + 1);
                if (startIndex + index + 1 > _buffer.Length)
                    return -1;
                if (data.Contains(_buffer[startIndex + index]))
                    return index;
                ++index;
            }
        }

        public byte[] ReadLine()
        {
            var found = IndexAnyOf(new[] { '\r', '\n' });
            if (found < 0)
                return ReadAll();
            if (StartsWith("\r\n", found))
                return Read(found + 2);
            else if (StartsWith("\r", found))
                return Read(found + 1);
            else if (StartsWith("\n", found))
                return Read(found + 1);
            else
                throw new Exception("内部エラーです。");
        }

        public byte[] ReadNewLine()
        {
            if (StartsWith("\r\n"))
                return Read(2);
            else if (StartsWith("\r"))
                return Read(1);
            else if (StartsWith("\n"))
                return Read(1);
            else
                throw new Exception("内部エラーです。");
        }

        public byte[] ReadAll()
        {
            var length = 1024;
            while (true)
            {
                ReadData(length);
                if (_buffer.Length != length)
                    break;
                length += 1024;
            }
            var newBuffer = new byte[_buffer.Length];
            _buffer.CopyTo(newBuffer, 0);
            Drop(newBuffer.Length);
            return newBuffer;
        }

        public byte[] Read(int length)
        {
            ReadData(length);
            var newBuffer = new byte[Math.Min(length, _buffer.Length)];
            Array.Copy(_buffer, 0, newBuffer, 0, newBuffer.Length);
            Drop(newBuffer.Length);
            _pos += newBuffer.Length;
            return newBuffer;
        }

        public void DropNewLine()
        {
            if (StartsWith("\r\n"))
                Drop(2);
            else if (StartsWith("\r"))
                Drop(1);
            else if (StartsWith("\n"))
                Drop(1);
            else
                throw new Exception(string.Format("改行が見つかりません。: pos={0}", Position));
        }

        public void Drop(int length)
        {
            ReadData(length);
            if (length > _buffer.Length)
                throw new Exception(string.Format("内部エラーです。: pos={0}", Position));
            var newBuffer = new byte[_buffer.Length - length];
            Array.Copy(_buffer, length, newBuffer, 0, _buffer.Length - length);
            _pos += length;
            _buffer = newBuffer;
        }

        public int Position => _pos;

        public override string ToString()
        {
            return "(" + Encoding.ASCII.GetString(_buffer, 0, Math.Min(_buffer.Length, 32)) + "..." + ")";
        }

        private void ReadData(int length)
        {
            if (_buffer.Length >= length)
                return;
            if (length - _buffer.Length < 1024)
                length = _buffer.Length + 10240;
            var count = _buffer.Length;
            Array.Resize(ref _buffer, length);
            while (count < length)
            {
                int result = _stream.Read(_buffer, count, length - count);
                if (result <= 0)
                {
                    _is_eof = true;
                    break;
                }
                count += result;
            }
            Array.Resize(ref _buffer, count);
        }
    }
}
