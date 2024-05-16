import urllib.request
import os
import UnityPy
import gzip
import io
from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
from cryptography.hazmat.backends import default_backend
import sys
import json
import shutil
import time

import argparse
def decrypt_aes_cbc(key: str, iv: str, data: bytes) -> bytes:
    key_bytes = key.encode('utf-8')
    iv_bytes = iv.encode('utf-8')

    cipher = Cipher(algorithms.AES(key_bytes), modes.CBC(iv_bytes), backend=default_backend())
    decryptor = cipher.decryptor()

    decrypted_data = decryptor.update(data) + decryptor.finalize()
    return decrypted_data[:-decrypted_data[-1]]

name = "Sound.bytes"

with open(name, "rb") as f:
    zipped = decrypt_aes_cbc("akmzncej3dfheuds654sg9ad1f3fnfoi", "lmxcye89bsdfb0a1", bytes(f.read()))
    with open(name + ".bin", "wb") as f2:
        f2.write(zipped)
