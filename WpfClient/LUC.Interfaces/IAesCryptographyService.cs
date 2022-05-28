using System;

namespace LUC.Interfaces
{
    public interface IAesCryptographyService
    {
        Byte[] GenerateRandomKey();

        Byte[] Encrypt( Byte[] plainData, Byte[] key, Byte[] iv );
        String Encrypt( String plainText, Byte[] key, Byte[] iv );

        Byte[] Decrypt( Byte[] encryptedData, Byte[] key, Byte[] iv );
        String Decrypt( String encryptedText, Byte[] key, Byte[] iv );
    }
}
