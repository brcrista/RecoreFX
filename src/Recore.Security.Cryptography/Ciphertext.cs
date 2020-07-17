﻿using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Recore.Security.Cryptography
{
    /// <summary>
    /// A strongly-typed representation of a string passed through a cryptographic hash function.
    /// </summary>
    /// <remarks>
    /// Use this type on fields that you want to be sure get encrypted.
    /// The .NET type system will make it impossible for a plaintext string to be assigned to that field.
    /// </remarks>
    public sealed class Ciphertext<THash> : IEquatable<Ciphertext<THash>> where THash : HashAlgorithm
    {
        /// <summary>
        /// The ciphertext as a string.
        /// </summary>
        public string Value { get; }

        private Ciphertext(string value)
        {
            Value = value;
        }

        /// <summary>
        /// The ciphertext as a string.
        /// </summary>
        public override string ToString() => Value;

        /// <summary>
        /// Compares this <see cref="Ciphertext{THash}"/>
        /// to another object for equality.
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is Ciphertext<THash> cipherText
                && Equals(cipherText);
        }

        /// <summary>
        /// Compares two instances of <see cref="Ciphertext{THash}"/>
        /// for equality.
        /// </summary>
        public bool Equals(Ciphertext<THash> other)
        {
            return other != null
                && Value == other.Value;
        }

        /// <summary>
        /// Returns the hash code of the underlying value.
        /// </summary>
        public override int GetHashCode() => Value.GetHashCode();

        /// <summary>
        /// Hashes a plaintext string to create an instance of <see cref="Ciphertext{THash}"/>.
        /// </summary>
        /// <param name="plaintext">The string to encrypt.</param>
        /// <param name="salt">
        /// A cryptographic salt to append to the plaintext.
        /// This is used to protect the hashing algorithm from being broken by a rainbow table.
        /// However, it cannot protect easily guessed plaintexts.
        /// </param>
        /// <param name="hash">An instance of the hashing algorithm to apply to the plaintext.</param>
        /// <remarks>
        /// <see cref="Ciphertext{THash}"/> uses a factory method because hashing can be an expensive operation.
        /// In the future, this operation will be asynchronous.
        /// The <paramref name="hash"/> parameter is needed because there is no generic way to create an instance of hashing algorithm.
        /// The extension methods in <see cref="Ciphertext"/> fill in this parameter
        /// for their respective hashing algorithms.
        /// Those methods should be preferred to this one unless finer-grained control is needed.
        /// </remarks>
        public static Ciphertext<THash> Encrypt(string plaintext, byte[] salt, THash hash)
        {
            if (plaintext == null)
            {
                throw new ArgumentNullException(nameof(plaintext));
            }

            if (salt == null)
            {
                throw new ArgumentNullException(nameof(salt));
            }

            if (hash == null)
            {
                throw new ArgumentNullException(nameof(hash));
            }

            var plaintextBytes = Encoding.Unicode.GetBytes(plaintext);
            var saltedPlaintextBytes = plaintextBytes.Concat(salt).ToArray();
            var hashBytes = hash.ComputeHash(saltedPlaintextBytes);
            return new Ciphertext<THash>(Convert.ToBase64String(hashBytes));
        }

        /// <summary>
        /// Determines whether two instances of <see cref="Ciphertext{THash}"/>
        /// have the same value.
        /// </summary>
        public static bool operator ==(Ciphertext<THash> lhs, Ciphertext<THash> rhs) => Equals(lhs, rhs);

        /// <summary>
        /// Determines whether two instances of <see cref="Ciphertext{THash}"/>
        /// have different values.
        /// </summary>
        public static bool operator !=(Ciphertext<THash> lhs, Ciphertext<THash> rhs) => !Equals(lhs, rhs);
    }

    /// <summary>
    /// Provides helper methods for working with <see cref="Ciphertext{THash}"/>.
    /// </summary>
    /// <remarks>
    /// This type exists because of constraints with generics in .NET.
    /// Implementations of <see cref="HashAlgorithm"/> are conventionally created through a
    /// factory method like <see cref="HashAlgorithm.Create()"/>.
    /// However, you can't call a static method on a type parameter.
    /// </remarks>
    public static class Ciphertext
    {
        /// <summary>
        /// Encrypts the plaintext with the MD5 hashing algorithm.
        /// </summary>
        public static Ciphertext<MD5> MD5(string plaintext, byte[] salt)
        {
            using (var hash = System.Security.Cryptography.MD5.Create())
            {
                return Ciphertext<MD5>.Encrypt(plaintext, salt, hash);
            }
        }

        /// <summary>
        /// Encrypts the plaintext with the SHA1 hashing algorithm.
        /// </summary>
        public static Ciphertext<SHA1> SHA1(string plaintext, byte[] salt)
        {
            using (var hash = System.Security.Cryptography.SHA1.Create())
            {
                return Ciphertext<SHA1>.Encrypt(plaintext, salt, hash);
            }
        }

        /// <summary>
        /// Encrypts the plaintext with the SHA256 hashing algorithm.
        /// </summary>
        public static Ciphertext<SHA256> SHA256(string plaintext, byte[] salt)
        {
            using (var hash = System.Security.Cryptography.SHA256.Create())
            {
                return Ciphertext<SHA256>.Encrypt(plaintext, salt, hash);
            }
        }
    }
}
