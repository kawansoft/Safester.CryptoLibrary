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

using Org.BouncyCastle.Bcpg.OpenPgp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Safester.CryptoLibrary.Api
{
    /// <summary>
    /// Allows to create a PgpPublicKey from a Stream.
    /// </summary>
    public class PgpPublicKeyGetter
    {
        /// <summary>
        /// Creates an OpenPGP PgpPublicKey using a string that contains a keyring.
        /// </summary>
        /// <param name="keyring">the armored keyring string that contains the PgpPublicKey</param>
        /// <returns>A PgpPublicKey</returns>
        public static PgpPublicKey ReadPublicKey(
            String keyring)
        {
            if (keyring == null)
            {
                throw new ArgumentNullException("keyring can not be null!");
            }

            byte[] bytes = Encoding.UTF8.GetBytes(keyring);
            MemoryStream memoryStream = new MemoryStream(bytes);
            return ReadPublicKey(memoryStream);
        }

        /// <summary>
        /// Creates an OpenPGP PgpPublicKey using an input stream
        /// </summary>
        /// <param name="inputStream">the input stream that contains the PgpPublicKey</param>
        /// <returns>A PgpPublicKey</returns>
        public static PgpPublicKey ReadPublicKey(Stream inputStream)
        {
            if (inputStream == null)
            {
                throw new ArgumentNullException("inputStream can not be null!");
            }

            try
            {
                inputStream = PgpUtilities.GetDecoderStream(inputStream);

                PgpPublicKeyRingBundle pgpPub = new PgpPublicKeyRingBundle(inputStream);

                //
                // we just loop through the collection till we find a key suitable for encryption, in the real
                // world you would probably want to be a bit smarter about this.
                //

                //
                // iterate through the key rings.
                //

                foreach (PgpPublicKeyRing kRing in pgpPub.GetKeyRings())
                {
                    foreach (PgpPublicKey k in kRing.GetPublicKeys())
                    {
                        if (k.IsEncryptionKey)
                        {
                            return k;
                        }
                    }
                }

                throw new ArgumentException("Can't find encryption PgpPublicKey key in key ring.");
            }
            finally
            {
                inputStream.Dispose();
            }
        }
    }
}
