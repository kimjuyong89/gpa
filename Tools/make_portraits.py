"""받아 둔 LinXueLian 캐릭터 팩에서 초상화와 전투 입상을 만든다. Pillow 필요.
팩은 C:/dev/Assets/Unpacked 에 풀려 있어야 한다. 실행: python Tools/make_portraits.py
결과: Art/Resources/Portraits (대화·메뉴), Art/Resources/Bodies (전투)"""
import os
from PIL import Image, ImageEnhance, ImageOps

U = 'C:/dev/Assets/Unpacked/'
PORTRAITS = 'Assets/_Project/Art/Resources/Portraits/'
BODIES = 'Assets/_Project/Art/Resources/Bodies/'
PORTRAIT_SIZE = 256
BODY_HEIGHT = 440


def pick(pack, words=('normal', 'neutral', 'smile', 'base', 'happy')):
    files = sorted(os.path.join(r, f) for r, _, fs in os.walk(U + pack) for f in fs if f.lower().endswith('.png'))
    for w in words:
        for f in files:
            if w in os.path.basename(f).lower():
                return f
    return files[0]


def body(pack, kid=None):
    """투명 배경 입상. kid를 주면 세 아이 그림에서 한 명만 자른다."""
    im = Image.open(pick(pack)).convert('RGBA')
    l, t, r, b = im.getbbox()
    if kid is not None:
        w = (r - l) // 3
        im = im.crop((l + kid * w, t, l + (kid + 1) * w, b))
    return im.crop(im.getbbox())


def head(im, frac=0.46):
    """입상의 머리 쪽을 정사각형으로 자른다."""
    w, h = im.size
    side = min(w, int(h * frac))
    top = im.crop((0, 0, w, side // 2)).getbbox()
    cx = (top[0] + top[2]) // 2
    x = max(0, min(w - side, cx - side // 2))
    return im.crop((x, 0, x + side, side))


def ink(im, dark=0.42, tint=(38, 34, 30)):
    """먹빛으로 물들인다: 색을 빼고 어둡게 눌러 이름 없는 적의 그림으로 쓴다."""
    r, g, b, a = im.split()
    gray = ImageOps.grayscale(Image.merge('RGB', (r, g, b)))
    gray = ImageEnhance.Contrast(gray).enhance(1.25).point(lambda v: int(v * dark))
    tinted = ImageOps.colorize(gray, black=tint, white=(210, 200, 182))
    return Image.merge('RGBA', (*tinted.split(), a))


def fade_feet(im, part=0.16):
    """입상은 허벅지에서 잘려 있어 아랫단을 투명하게 풀어 바닥에 녹인다."""
    a = im.getchannel('A')
    start = int(im.height * (1 - part))
    for y in range(start, im.height):
        k = 1 - (y - start) / (im.height - start)
        a.paste(a.crop((0, y, im.width, y + 1)).point(lambda v: int(v * k)), (0, y))
    im.putalpha(a)
    return im


yang_champion = body('2TIAN_WANDERING_CHAMPION_BASE_PACK')
yang_butterfly = body('2TIAN_BUTTERFLY_DREAM_BASE_PACK')
yang_celestial = body('2TIAN_WANDERING_CELESTIAL_PALACE_BASE_PACK')

# 이름 = Combatant 이름. 대본 화자 이름과도 같다.
FIGURES = {
    '주인공1': body('MRY_BAMBOOHERO_SPRITE_PACK'),
    '주인공2': body('BUTTERFLY_DREAM_BASE_PACK'),
    '주인공3': body('GRAND_PEONY_BASE_PACK'),
    '주인공4': body('GOLD_SECT_BASE_PACK'),
    '고진악': body('MRY_DRAGONHERO_SPRITE_PACK'),
    '소육': body('VILLAGE_CHILDREN_WX_BLUE', kid=0),
    '창고지기': yang_champion,
    '도적 두목': yang_butterfly,
    '흑사방 졸개 갑': ink(yang_champion),
    '흑사방 졸개 을': ink(yang_butterfly),
    '갈대밭 도적': ink(yang_champion, 0.6, (46, 38, 30)),
}
# 영입 후보
FIGURES['석두'] = body('VILLAGE_CHILDREN_WX_MIX_1', kid=0)
FIGURES['운학'] = body('VILLAGE_CHILDREN_WX_MIX_1', kid=2)
FIGURES['연아'] = body('VILLAGE_CHILDREN_WX_DUSKY', kid=2)
# 여자 동료 다섯. ARAVIND 한 인물의 의상·머리색 변형이라 얼굴이 같다. 인물별 그림은 구할 때 바꾼다.
FIGURES['백설화'] = body('ARAVIND_XIANXIA_GREENTEA')
FIGURES['홍련'] = body('ARAVIND_XIANXIA_SHRINEGUARD')
FIGURES['묵향'] = body('ARAVIND_XIANXIA_DEMONSECT')
FIGURES['유소소'] = body('ARAVIND_XIANXIA_BLOSSOM')
FIGURES['능소'] = body('ARAVIND_XIANXIA_SEABLUE')
# 폐관수련의 심마: 주인공을 먹과 핏빛으로 물들인 그림자
for i in range(1, 5):
    FIGURES[f'심마{i}'] = ink(FIGURES[f'주인공{i}'], 0.4, (54, 18, 16))
# 전투에 나오지 않는 인물은 초상화만
ONLY_PORTRAIT = {
    '윤백산': body('MOB_OLD_MAN_GREEN_PACK'),
    '은둔 노인': body('MOB_OLD_MAN_RED_PACK'),
    '장터 상인': body('MOB_OLD_MAN_GOLDLOTUS_PACK'),
    '탁웅': yang_celestial,
    '점소이': body('VILLAGE_CHILDREN_WX_GREEN', kid=2),
    '흑사방 졸개': ink(yang_champion),
    '흑사방 연락책': ink(body('GOLD_SECT_BASE_PACK')),
}

KIDS = ('소육', '점소이', '석두', '운학', '연아')
os.makedirs(PORTRAITS, exist_ok=True)
os.makedirs(BODIES, exist_ok=True)
for name, im in {**FIGURES, **ONLY_PORTRAIT}.items():
    frac = 0.62 if name in KIDS else 0.46
    head(im, frac).resize((PORTRAIT_SIZE, PORTRAIT_SIZE), Image.LANCZOS).save(PORTRAITS + name + '.png')
for name, im in FIGURES.items():
    h = int(BODY_HEIGHT * 0.72) if name in KIDS else BODY_HEIGHT
    fade_feet(im.resize((im.width * h // im.height, h), Image.LANCZOS)).save(BODIES + name + '.png')
print(len(FIGURES), 'bodies')
