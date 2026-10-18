# -*- coding: utf-8 -*-
import os
import qrcode
from PIL import Image
from qrcode.image.styledpil import StyledPilImage
from qrcode.image.styles.moduledrawers import RoundedModuleDrawer
from qrcode.image.styles.colormasks import SolidFillColorMask

def generate_artistic_qr(payload, is_wechat, out_path):
    qr = qrcode.QRCode(
        version=None,
        error_correction=qrcode.constants.ERROR_CORRECT_M, # Crisp & clean
        box_size=12,
        border=3,
    )
    qr.add_data(payload)
    qr.make(fit=True)

    # 翡翠绿 vs 电光蓝
    front_color = (0, 168, 68) if is_wechat else (18, 110, 240)
    back_color = (244, 250, 246) if is_wechat else (244, 247, 253)

    img = qr.make_image(
        image_factory=StyledPilImage,
        module_drawer=RoundedModuleDrawer(radius_ratio=0.75),
        color_mask=SolidFillColorMask(back_color=back_color, front_color=front_color)
    )

    img = img.convert("RGBA")
    w, h = img.size

    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    img.save(out_path, "PNG")
    print("Successfully generated clean QR: " + out_path + " (" + str(w) + "x" + str(h) + ")")

if __name__ == "__main__":
    wechat_payload = "wxp://f2f007hUP3XwZnW6uOm8Kaimr7xolOqcVrBB9u7iii4NVrA"
    alipay_payload = "https://qr.alipay.com/fkx17893zwdlbcsqncjh4b4"

    generate_artistic_qr(wechat_payload, True, "resources/donate_wechat.png")
    generate_artistic_qr(alipay_payload, False, "resources/donate_alipay.png")
