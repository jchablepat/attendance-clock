using System;
using System.Security.Cryptography;
using System.IO;
using System.Text;


namespace VTACheckClock.Services.Libs
{
    /// <summary>
    /// Clase para encripción reversible con algoritmo AES, y para tratamiento y conversión de cadenas de texto y arreglos de bytes.
    /// Aes es la implementación moderna y recomendada para el algoritmo AES (Advanced Encryption Standard)
    /// </summary>
    public class SimpleAES
    {
        // Asegúrate de que Key y Vector están definidos correctamente en tu clase
        private readonly byte[] Key = [111, 99, 86, 119, 105, 97, 101, 48, 101, 86, 115, 116, 99, 77, 56, 45, 84, 109, 84, 114, 49, 101, 40, 105, 120, 102, 111, 83, 41, 46, 50, 4];
        private readonly byte[] Vector = [121, 115, 110, 84, 101, 65, 95, 83, 86, 116, 116, 110, 97, 100, 101, 99];
        private readonly ICryptoTransform EncryptorTransform;
        private readonly ICryptoTransform DecryptorTransform;
        private readonly UTF8Encoding UTFEncoder;

        public SimpleAES()
        {
            using (Aes aes = Aes.Create()) {
                aes.Key = this.Key;
                aes.IV = this.Vector;

                EncryptorTransform = aes.CreateEncryptor();
                DecryptorTransform = aes.CreateDecryptor();
            }

            UTFEncoder = new UTF8Encoding();
        }

        public static byte[] GenerateEncryptionKey()
        {
            using Aes aes = Aes.Create();
            aes.GenerateKey();
            return aes.Key;
        }

        public static byte[] GenerateEncryptionVector()
        {
            using Aes aes = Aes.Create();
            aes.GenerateIV();
            return aes.IV;
        }

        public string EncryptToString(string TextValue)
        {
            return ByteArrToString(Encrypt(TextValue));
        }

        public byte[] Encrypt(string TextValue)
        {
            Byte[] bytes = UTFEncoder.GetBytes(TextValue);
            MemoryStream memoryStream = new();
            CryptoStream cs = new(memoryStream, EncryptorTransform, CryptoStreamMode.Write);

            cs.Write(bytes, 0, bytes.Length);
            cs.FlushFinalBlock();
            memoryStream.Position = 0;
            byte[] encrypted = new byte[memoryStream.Length];
            memoryStream.Read(encrypted, 0, encrypted.Length);
            cs.Close();
            memoryStream.Close();

            return encrypted;
        }

        public string DecryptString(string EncryptedString)
        {
            return Decrypt(StrToByteArray(EncryptedString)!)!;
        }

        public string? Decrypt(byte[] EncryptedValue)
        {
            if (EncryptedValue == null) return null;

            MemoryStream encryptedStream = new();
            CryptoStream decryptStream = new(encryptedStream, DecryptorTransform, CryptoStreamMode.Write);

            decryptStream.Write(EncryptedValue, 0, EncryptedValue.Length);
            decryptStream.FlushFinalBlock();
            encryptedStream.Position = 0;
            Byte[] decryptedBytes = new Byte[encryptedStream.Length];
            encryptedStream.Read(decryptedBytes, 0, decryptedBytes.Length);
            encryptedStream.Close();

            var result = UTFEncoder.GetString(decryptedBytes);
            return result;
        }

        public static byte[]? StrToByteArray(string? str)
        {
            if (str.Length == 0)
                //throw new Exception("Invalid string value in StrToByteArray");
                return null;

            byte val;
            byte[] byteArr = new byte[str.Length / 3];
            int i = 0;
            int j = 0;

            do {
                val = byte.Parse(str.Substring(i, 3));
                byteArr[j++] = val;
                i += 3;
            } while (i < str.Length);

            return byteArr;
        }

        public static string ByteArrToString(byte[] byteArr)
        {
            if(byteArr != null) {
                int jjj = byteArr.Length;
                string[] pre_resp = new string[jjj];

                for (int iii = 0; iii < jjj; iii++)
                {
                    pre_resp[iii] = byteArr[iii].ToString("000");
                }

                return string.Join("", pre_resp);
            }
            return "";
        }

        public static byte[] ToLiteralBytes(string str)
        {
            if (string.IsNullOrEmpty(str) || (str.Length <= 0)) {
                return [];
            } else {
                byte[] byteArr = new byte[str.Length / 3];
                int i = 0;
                int j = 0;

                do {
                    string conv = (str.Substring(i, 3));
                    byteArr[j++] = Convert.ToByte(conv);
                    i += 3;
                } while (i < str.Length);

                return byteArr;
            }
        }

        public byte[] EncryptToBytes(string? str)
        {
            return ToLiteralBytes(EncryptToString(str!));
        }

        public string EncryptToHexBytes(string str)
        {
            byte[] byte_chain = EncryptToBytes(@"" + str);
            int jjj = byte_chain.Length;
            string[] pre_resp = new string[jjj];

            for (int iii = 0; iii < jjj; iii++)
            {
                pre_resp[iii] = byte_chain[iii].ToString("X2");
            }

            return string.Join("", pre_resp);
        }

        public string DecryptFromHexBytes(string str)
        {
            byte val;
            byte[] byteArr = new byte[str.Length / 2];
            int i = 0;
            int j = 0;

            do {
                val = byte.Parse(str.Substring(i, 2), System.Globalization.NumberStyles.HexNumber);
                byteArr[j++] = val;
                i += 2;
            } while (i < str.Length);

            return DecryptFromBytes(byteArr);
        }

        public string DecryptFromBytes(byte[] bytes)
        {
            return Decrypt(StrToByteArray(ByteArrToString(bytes))!)!;
        }

        public byte[] TransToBytes(string la_str)
        {
            return UTFEncoder.GetBytes(la_str);
        }

        public string TransToStr(byte[] los_bytes)
        {
            return UTFEncoder.GetString(los_bytes);
        }

        public static string ToByteChain(byte[] bytes)
        {
            string la_resp = "";

            foreach (byte el_byte in bytes)
            {
                la_resp += el_byte.ToString("000");
            }

            return la_resp;
        }

        /// <summary>
        /// Convierte un array de bytes en una cadena hexadecimal.
        /// 
        /// Utiliza un bucle para recorrer cada byte en el array y los convierte en su representación hexadecimal de dos caracteres (usando el formato "X2") antes de concatenarlos en una cadena y retornarla.
        /// </summary>
        /// <param name="bytes">El array de bytes a convertir.</param>
        /// <returns>Una cadena que representa los bytes en formato hexadecimal.</returns>
        public static string ToHexByteChain(byte[] bytes)
        {
            string la_resp = string.Empty;

            foreach (byte el_byte in bytes)
            {
                la_resp += el_byte.ToString("X2");
            }

            return la_resp;
        }

        public static byte[] ToHexBytes(string hex_str)
        {
            if (string.IsNullOrEmpty(hex_str) || (hex_str.Length <= 0)) {
                return [];
            } else {
                byte[] byteArr = new byte[hex_str.Length / 2];
                int i = 0;
                int j = 0;

                do {
                    string conv = (hex_str.Substring(i, 2));
                    byteArr[j++] = Convert.ToByte(int.Parse(conv, System.Globalization.NumberStyles.HexNumber));
                    i += 2;
                }
                while (i < hex_str.Length);

                return byteArr;
            }
        }
    }
}
