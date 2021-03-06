﻿/* 
 * This file is part of Safester C# OpenPGP SDK.                                
 * Copyright(C) 2019,  KawanSoft SAS
 * (https://www.safester.net). All rights reserved.                                
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License. 
 */

using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Org.BouncyCastle.Crypto.Generators;
using Safester.CryptoLibrary.Src.Api.Util;
using Safester.CryptoLibrary.Api.Util;
using Safester.CryptoLibrary.Src.Api;
using Safester.CryptoLibrary.Src.Api.Util.ElGamal;

namespace Safester.CryptoLibrary.Api
{
    /// <summary>
    /// Allows to generate a PGP Key Pair. 
    /// Will create a string Base64 armored keyring for both the
    /// private and public PGP key.
    /// </summary>
    public class PgpKeyPairGenerator
    {

        /// Member values 
        private string identity = null;
        private char[] passphrase = null;
        private PublicKeyAlgorithm publicKeyAlgorithm = PublicKeyAlgorithm.RSA;
        private PublicKeyLength publicKeyLength = PublicKeyLength.BITS_2048;


        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="identity">Email of key pair owner</param>
        /// <param name="passphrase">Passphrase of key pair</param>
        /// <param name="publicKeyAlgorithm">PublicKeyAlgorithm.DSA_ELGAMAL or PublicKeyAlgorithm.RSA</param>
        /// <param name="publicKeyLength">PublicKeyLength.BITS_1024, PublicKeyLength.BITS_2048, PublicKeyLength.BITS_3072,,</param>
        public PgpKeyPairGenerator(string identity, char[] passphrase, PublicKeyAlgorithm publicKeyAlgorithm, PublicKeyLength publicKeyLength) 
        {
            this.identity = identity ?? throw new ArgumentNullException("identity can not be null!");
            this.passphrase = passphrase ?? throw new ArgumentNullException("passphrase can not be null!");

            this.publicKeyAlgorithm = publicKeyAlgorithm;
            this.publicKeyLength = publicKeyLength;
        }

        /// <summary>
        /// Generates the key pair on keyring on output streams. Streams are closed my method.
        /// </summary>
        /// <param name="outSecret">the private/secret key keyring output stream</param>
        /// <param name="outPublic">the public key keyring output stream</param>
        public void Generate(Stream outSecret, Stream outPublic)
        {
            if (outSecret == null)
            {
                throw new ArgumentNullException("outSecret can not be null!");
            }

            if (outPublic == null)
            {
                throw new ArgumentNullException("outPublic can not be null!");
            }

            try
            {
                if (publicKeyAlgorithm == PublicKeyAlgorithm.RSA)
                {
                    GenerateRsa(outSecret, outPublic);
                }
                else
                {
                    GenerateElGamal(outSecret, outPublic);
                }
            }
            finally
            {
                outSecret.Dispose();
                outPublic.Dispose();
            }
        }


        /// <summary>
        /// Generates the armored private and public keyrings. 
        /// </summary>
        /// <returns>The PgpKeyPairHolder that contains armored private/secret keyring and armored public keyring </returns>
        public PgpKeyPairHolder Generate()
        {
            MemoryStream outSecret = new MemoryStream();
            MemoryStream outPublic = new MemoryStream();

            if (publicKeyAlgorithm == PublicKeyAlgorithm.RSA)
            {
                GenerateRsa(outSecret, outPublic);
            }
            else
            {
                GenerateElGamal(outSecret, outPublic);
            }

            string secretKeyRing = Encoding.UTF8.GetString(outSecret.ToArray(), 0, (int)outSecret.Length);
            string publicKeyRing = Encoding.UTF8.GetString(outPublic.ToArray(), 0, (int)outPublic.Length);
            PgpKeyPairHolder pgpKeyPairHolder = new PgpKeyPairHolder(secretKeyRing, publicKeyRing);
            return pgpKeyPairHolder;
        }

        private void GenerateRsa(Stream outSecret, Stream outPublic)
        {
            IAsymmetricCipherKeyPairGenerator kpg = GeneratorUtilities.GetKeyPairGenerator("RSA");

            // Prepare a strong Secure Random with seed
            SecureRandom secureRandom = PgpEncryptionUtil.GetSecureRandom();

            kpg.Init(new RsaKeyGenerationParameters(
                Org.BouncyCastle.Math.BigInteger.ValueOf(0x10001), secureRandom, (int) publicKeyLength, 25));

            AsymmetricCipherKeyPair kp = kpg.GenerateKeyPair();
            RsaKeyGeneratorUtil.ExportKeyPair(outSecret, outPublic, kp.Public, kp.Private, identity, passphrase, true);
        }

        private void GenerateElGamal(Stream outSecret, Stream outPublic)
        {
            // Prepare a strong Secure Random with seed
            SecureRandom secureRandom = PgpEncryptionUtil.GetSecureRandom();

            IAsymmetricCipherKeyPairGenerator dsaKpg = GeneratorUtilities.GetKeyPairGenerator("DSA");
            DsaParametersGenerator pGen = new DsaParametersGenerator();
            pGen.Init((int) PublicKeyLength.BITS_1024, 80, new SecureRandom()); // DSA is 1024 even for long 2048+ ElGamal keys 
            DsaParameters dsaParams = pGen.GenerateParameters();
            DsaKeyGenerationParameters kgp = new DsaKeyGenerationParameters(secureRandom, dsaParams);
            dsaKpg.Init(kgp);

            //
            // this takes a while as the key generator has to Generate some DSA parameters
            // before it Generates the key.
            //
            AsymmetricCipherKeyPair dsaKp = dsaKpg.GenerateKeyPair();
            IAsymmetricCipherKeyPairGenerator elgKpg = GeneratorUtilities.GetKeyPairGenerator("ELGAMAL");

            Group elgamalGroup = Precomputed.GetElGamalGroup((int) this.publicKeyLength);

            if (elgamalGroup == null)
            {
                throw new ArgumentException("ElGamal Group not found for key length: " + this.publicKeyLength);
            }

            Org.BouncyCastle.Math.BigInteger p = elgamalGroup.GetP();
            Org.BouncyCastle.Math.BigInteger g = elgamalGroup.GetG();

            secureRandom = PgpEncryptionUtil.GetSecureRandom();
            ElGamalParameters elParams = new ElGamalParameters(p, g);
            ElGamalKeyGenerationParameters elKgp = new ElGamalKeyGenerationParameters(secureRandom, elParams);
            elgKpg.Init(elKgp);

            //
            // this is quicker because we are using preGenerated parameters.
            //
            AsymmetricCipherKeyPair elgKp = elgKpg.GenerateKeyPair();
            DsaElGamalKeyGeneratorUtil.ExportKeyPair(outSecret, outPublic, dsaKp, elgKp, identity, passphrase, true);
        }

    }
}
