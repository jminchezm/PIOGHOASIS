using System.Security.Cryptography;

namespace PIOGHOASIS.Helpers
{
    public static class Pbkdf2
    {
        private const int Iterations = 200_000; // recomendado hoy
        private const int SaltSize = 16;        // 128 bits
        private const int KeySize = 32;        // 256 bits

        public static byte[] HashPassword(string password)
        {
            // sal aleatoria
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);

            // subclave
            byte[] subkey = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                Iterations,
                HashAlgorithmName.SHA256,
                KeySize
            );

            // concatenamos salt + subkey
            byte[] output = new byte[SaltSize + KeySize];
            Buffer.BlockCopy(salt, 0, output, 0, SaltSize);
            Buffer.BlockCopy(subkey, 0, output, SaltSize, KeySize);
            return output;
        }

        public static bool Verify(string password, byte[] stored)
        {
            if (stored is null || stored.Length != (SaltSize + KeySize)) return false;

            // extrae sal y hash almacenado
            byte[] salt = new byte[SaltSize];
            byte[] saved = new byte[KeySize];
            Buffer.BlockCopy(stored, 0, salt, 0, SaltSize);
            Buffer.BlockCopy(stored, SaltSize, saved, 0, KeySize);

            // recalcula
            byte[] computed = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                Iterations,
                HashAlgorithmName.SHA256,
                KeySize
            );

            // comparación constante
            return CryptographicOperations.FixedTimeEquals(computed, saved);
        }
    }
}
