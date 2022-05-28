using LUC.Interfaces;

using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace LUC.Services.Implementation
{
    [Export( typeof( IAesCryptographyService ) )]

    public class AesCryptographyService : IAesCryptographyService
    {
        public Byte[] Decrypt( Byte[] encryptedData, Byte[] key, Byte[] iv )
        {
            using ( var aes = new AesCryptoServiceProvider() )
            {
                aes.KeySize = 128;
                aes.BlockSize = 128;
                aes.Padding = PaddingMode.Zeros;

                aes.Key = key;
                aes.IV = iv;

                using ( ICryptoTransform decryptor = aes.CreateDecryptor( aes.Key, aes.IV ) )
                {
                    return PerformCryptography( encryptedData, decryptor );
                }
            }
        }

        public String Decrypt( String encryptedText, Byte[] key, Byte[] iv )
        {
            Byte[] encryptedData = Encoding.Unicode.GetBytes( encryptedText );

            // Check arguments. 
            if ( encryptedData == null || encryptedData.Length <= 0 )
            {
                throw new ArgumentNullException( String.Empty, "cipherText is null" );
            }

            if ( key == null || key.Length <= 0 )
            {
                throw new ArgumentNullException( String.Empty, "Key is null" );
            }

            if ( iv == null || iv.Length <= 0 )
            {
                throw new ArgumentNullException( String.Empty, "IV is null" );
            }

            // Declare the string used to hold 
            // the decrypted text. 
            String plaintext = null;

            // Create an RijndaelManaged object 
            // with the specified key and IV. 
            using ( var rijAlg = new RijndaelManaged() )
            {
                rijAlg.Key = key;
                rijAlg.IV = iv;

                // Create a decrytor to perform the stream transform.
                ICryptoTransform decryptor = rijAlg.CreateDecryptor( rijAlg.Key, rijAlg.IV );

                // Create the streams used for decryption. 
                using ( var msDecrypt = new MemoryStream( encryptedData ) )
                {
                    using ( var csDecrypt = new CryptoStream( msDecrypt, decryptor, CryptoStreamMode.Read ) )
                    {
                        using ( var srDecrypt = new StreamReader( csDecrypt ) )
                        {

                            // Read the decrypted bytes from the decrypting stream 
                            // and place them in a string.
                            plaintext = srDecrypt.ReadToEnd();
                        }
                    }
                }
            }

            return plaintext;
        }

        public Byte[] Encrypt( Byte[] plainData, Byte[] key, Byte[] iv )
        {
            using ( var aes = new AesCryptoServiceProvider() )
            {
                aes.KeySize = 128;
                aes.BlockSize = 128;
                aes.Padding = PaddingMode.Zeros;

                aes.Key = key;
                aes.IV = iv;

                using ( ICryptoTransform encryptor = aes.CreateEncryptor( aes.Key, aes.IV ) )
                {
                    return PerformCryptography( plainData, encryptor );
                }
            }
        }

        public String Encrypt( String plainText, Byte[] key, Byte[] iv )
        {
            // Check arguments. 
            if ( plainText == null || plainText.Length <= 0 )
            {
                throw new ArgumentNullException( String.Empty, "plainText is null" );
            }

            if ( key == null || key.Length <= 0 )
            {
                throw new ArgumentNullException( String.Empty, "Key is null" );
            }

            if ( iv == null || iv.Length <= 0 )
            {
                throw new ArgumentNullException( String.Empty, "IV is null" );
            }

            Byte[] encrypted;
            // Create an RijndaelManaged object 
            // with the specified key and IV. 
            using ( var rijAlg = new RijndaelManaged() )
            {
                rijAlg.Key = key;
                rijAlg.IV = iv;

                // Create a decryptor to perform the stream transform.
                ICryptoTransform encryptor = rijAlg.CreateEncryptor( rijAlg.Key, rijAlg.IV );

                // Create the streams used for encryption. 
                using ( var msEncrypt = new MemoryStream() )
                {
                    using ( var csEncrypt = new CryptoStream( msEncrypt, encryptor, CryptoStreamMode.Write ) )
                    {
                        using ( var swEncrypt = new StreamWriter( csEncrypt ) )
                        {
                            //Write all data to the stream.
                            swEncrypt.Write( plainText );
                        }

                        encrypted = msEncrypt.ToArray();
                    }
                }
            }

            // Return the encrypted bytes from the memory stream. 

            String result = Encoding.UTF8.GetString( encrypted );

            return result;
        }

        public Byte[] GenerateRandomKey()
        {
            using ( var aes = new AesCryptoServiceProvider() )
            {
                aes.KeySize = 128;
                aes.BlockSize = 128;
                aes.Padding = PaddingMode.Zeros;

                aes.GenerateKey();

                return aes.Key;
            }
        }

        private Byte[] PerformCryptography( Byte[] data, ICryptoTransform cryptoTransform )
        {
            using ( var ms = new MemoryStream() )
            {
                using ( var cryptoStream = new CryptoStream( ms, cryptoTransform, CryptoStreamMode.Write ) )
                {
                    cryptoStream.Write( data, 0, data.Length );
                    cryptoStream.FlushFinalBlock();

                    return ms.ToArray();
                }
            }
        }
    }
}
