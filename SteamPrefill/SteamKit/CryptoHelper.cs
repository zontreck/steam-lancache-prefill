﻿/*
 * This file is subject to the terms and conditions defined in
 * file 'license.txt', which is part of this source code package.
 */

namespace SteamPrefill.SteamKit
{
    /// <summary>
    /// Provides Crypto functions used in Steam protocols
    /// </summary>
    public static class CryptoHelper
    {
        /// <summary>
        /// Decrypts using AES/CBC/PKCS7 with an input byte array and key, using the random IV prepended using AES/ECB/None
        /// </summary>
        public static byte[] SymmetricDecrypt(byte[] input, byte[] key)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            return SymmetricDecrypt(input, key, out _);
        }
        
        /// <summary>
        /// Decrypts using AES/CBC/PKCS7 with an input byte array and key, using the random IV prepended using AES/ECB/None
        /// </summary>
        static byte[] SymmetricDecrypt(byte[] input, byte[] key, out byte[] iv)
        {
            using (var aes = Aes.Create())
            {
                aes.BlockSize = 128;
                aes.KeySize = 256;

                // first 16 bytes of input is the ECB encrypted IV
                byte[] cryptedIv = new byte[16];
                iv = new byte[cryptedIv.Length];
                Array.Copy(input, 0, cryptedIv, 0, cryptedIv.Length);

                // the rest is ciphertext
                byte[] cipherText = new byte[input.Length - cryptedIv.Length];
                Array.Copy(input, cryptedIv.Length, cipherText, 0, cipherText.Length);

                // decrypt the IV using ECB
                aes.Mode = CipherMode.ECB;
                aes.Padding = PaddingMode.None;

                using (var aesTransform = aes.CreateDecryptor(key, null))
                {
                    iv = aesTransform.TransformFinalBlock(cryptedIv, 0, cryptedIv.Length);
                }

                // decrypt the remaining ciphertext in cbc with the decrypted IV
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var aesTransform = aes.CreateDecryptor(key, iv))
                using (var ms = new MemoryStream(cipherText))
                using (var cs = new CryptoStream(ms, aesTransform, CryptoStreamMode.Read))
                {
                    // plaintext is never longer than ciphertext
                    byte[] plaintext = new byte[cipherText.Length];

                    int len = cs.ReadAll(plaintext);

                    byte[] output = new byte[len];
                    Array.Copy(plaintext, 0, output, 0, len);

                    return output;
                }
            }
        }

        /// <summary>
        /// Performs an Adler32 on the given input
        /// </summary>
        public static byte[] AdlerHash(byte[] input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            uint a = 0, b = 0;
            for (int i = 0; i < input.Length; i++)
            {
                a = (a + input[i]) % 65521;
                b = (b + a) % 65521;
            }
            return BitConverter.GetBytes(a | b << 16);
        }

    }
}
