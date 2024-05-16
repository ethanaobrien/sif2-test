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

b = 1
env = UnityPy.load("6572ca8348bc566b8cf01d43c4cc1b58.unity3d")
for obj in env.objects:
    print(obj.type.name)
    if obj.type.name == "TextAsset":
        data = obj.read()
        with open(data.name + ".bytes", "wb") as f:
            f.write(decrypt_aes_cbc("akmzncej3dfheuds654sg9ad1f3fnfoi", "lmxcye89bsdfb0a1", bytes(data.script)))
