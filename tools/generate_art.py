"""Rebuild En Silabas' geometric artwork. Requires Pillow; no downloaded art."""
from pathlib import Path
import stat
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1] / 'BeatEngine'
CONTENT = ROOT / 'BeatEngine.Core/Content'
FONT = CONTENT / 'Fonts/CONSOLA.TTF'
SYLLABLE_FONT = CONTENT / 'Fonts/RobotoMono-Bold.ttf'

def canvas(size, opaque=False):
    return Image.new('RGBA', size, 'white' if opaque else (255, 255, 255, 0))

def text(im, value, xy, size=48, fill='black', bold=False):
    d = ImageDraw.Draw(im)
    d.text(xy, value, font=ImageFont.truetype(str(SYLLABLE_FONT if bold else FONT), size), fill=fill, anchor='mm')

def cell(size=(220, 220), border=3):
    im = canvas(size, True)
    ImageDraw.Draw(im).rectangle((2, 2, size[0]-3, size[1]-3), outline='black', width=border)
    return im

def mark(size):
    im = canvas(size, True)
    d = ImageDraw.Draw(im)
    a, b = size[0] * .18, size[0] * .82
    for i in range(4):
        v = a + (b-a)*i/3
        d.line((v,a,v,b), fill='black', width=max(1,size[0]//100))
        d.line((a,v,b,v), fill='black', width=max(1,size[0]//100))
    text(im, 'e', (size[0]/2,size[1]/2), int(size[0]*.23))
    return im

def button(size, label):
    im = cell(size)
    text(im, label, (size[0]/2,size[1]/2), min(48, int(size[1]*.28)))
    return im

def title(size):
    im = canvas(size)
    logo = mark((240,240))
    im.alpha_composite(logo, ((size[0]-240)//2,40))
    text(im, 'En Silabas', (size[0]/2,360), 100)
    text(im, 'UN JUEGO DE PALABRAS', (size[0]/2,470), 28)
    return im

def tutorial(n):
    im = canvas((1080,1040), True)
    headings = ['Cada palabra empieza aquí.', 'Una sílaba por casilla.', 'Ordena. Une. Descubre.', 'A tu ritmo.']
    lines = [('Forma tres palabras con las sílabas.', 'Mostrar Pista revela una ayuda.'),
             ('Arrastra cada ficha a una fila.', 'Puedes moverla otra vez.'),
             ('Lee cada fila de izquierda a derecha.', 'Completa las tres palabras.'),
             ('Mostrar Pista resta 5 puntos por nivel.', 'Saltar una ronda resta 10 puntos.')]
    text(im, f'CÓMO JUGAR   /   0{n}', (540,70), 28)
    text(im, headings[n-1], (540,210), 45)
    for k, syllable in enumerate(['CA','MI','NO']):
        im.alpha_composite(cell(border=8), (180+k*240,380))
        text(im, syllable, (290+k*240,490), 54, bold=True)
    for j,line in enumerate(lines[n-1]):
        text(im,line,(540,730+j*70),32)
    for k in range(4):
        ImageDraw.Draw(im).ellipse((480+k*36,930,492+k*36,942), fill='black' if k==n-1 else 'white', outline='black', width=2)
    return im

def generate():
    # Replace every source visual, including unused legacy variants and platform art.
    paths = [p for p in ROOT.rglob('*') if p.suffix.lower() in ('.png','.jpg','.bmp','.ico') and not any(x in p.parts for x in ('bin','obj','ArtPreview'))]
    labels = {'Start':'Nueva partida', 'Continue':'Continuar', 'Entendido':'Siguiente', 'Exit':'Salir', 'Back':'Volver', 'Pause':'Pausa', 'Play':'Jugar', 'Retry':'Reintentar'}
    for p in paths:
        p.chmod(p.stat().st_mode | stat.S_IWRITE)
        with Image.open(p) as original:
            size = original.size
        name = p.stem
        if 'Backgrounds' in str(p):
            im = canvas((1200,2664), name.endswith('_0'))
        elif name.startswith('Tutorial'):
            im = tutorial(int(name[-1]))
        elif p.parent.name == 'Tiles':
            im = cell(border=8)
            if name == 'front':
                text(im, '·', (110,110), 64)
        elif name.startswith('SylabrixTitle'):
            im = title((1080,620))
        elif name == 'Welcome_button':
            im = canvas((1080,180))
            text(im,'PALABRAS. ORDEN. CALMA.',(540,90),30)
        elif name == 'ShowHint_button':
            im = button((600,120),'Mostrar Pista')
        elif name == 'Arrow':
            im = button((220,110),'Saltar >')
        elif p.parent.name == 'Buttons':
            im = button((1080,180),labels.get(name.split('_')[0], 'Continuar'))
        elif name == 'Panel':
            im = cell((1125,225))
            d = ImageDraw.Draw(im)
            for x in range(225,1125,225):
                d.line((x,2,x,222), fill='black', width=2)
        elif p.parent.name == 'Environment' or p.parent.name == 'Overlays':
            im = canvas((1080,360))
            label = {'Excelente':'Bien hecho.', 'GetReady':'Prepara tus palabras.', 'GetReady2':'Prepara tus palabras.', 'WatchCat':'Observa.', 'RepeatCat':'Tu turno.', 'you_win':'Bien hecho.', 'you_lose':'Inténtalo otra vez.'}.get(name,'En Silabas')
            text(im,label,(540,180),55)
        elif name == 'Star':
            im = canvas((416,32))
            d = ImageDraw.Draw(im)
            for i in range(13):
                x = i*32
                d.line((x+8,16,x+14,22,x+25,9), fill='black', width=2)
        elif name in ('Splash','Default'):
            im = canvas(size,True)
            logo = mark((min(size)//2,)*2)
            im.alpha_composite(logo,((size[0]-logo.width)//2,(size[1]-logo.height)//2))
        else:
            im = mark(size)
        if p.suffix.lower() in ('.jpg','.bmp'):
            im = im.convert('RGB')
        if p.suffix.lower() == '.ico':
            im.save(p, sizes=[(16,16),(32,32),(48,48),(128,128),(256,256)])
        else:
            temporary = p.with_name(p.stem + '.generated' + p.suffix)
            im.save(temporary)
            temporary.replace(p)
    print(f'Rebuilt {len(paths)} monochrome visual assets.')

def preview():
    """Compose reference layouts at the game's 1200 x 2664 design resolution."""
    out = ROOT / 'ArtPreview'
    out.mkdir(exist_ok=True)
    menu = canvas((1200,2664), True)
    for name, xy in [('Welcome_button',(60,180)), ('SylabrixTitle',(60,400)),
                     ('Start_button',(60,1250)), ('Continue_button',(60,1480))]:
        menu.alpha_composite(Image.open(CONTENT / f'UI/Buttons/{name}.png'),xy)
    board = canvas((1200,2664), True)
    text(board, 'PUNTOS: 0', (270,80), 60)
    board.alpha_composite(Image.open(CONTENT/'UI/Buttons/ShowHint_button.png'),(60,230))
    board.alpha_composite(Image.open(CONTENT/'UI/Buttons/Arrow.png'), (900,30))
    for y in (1140,870,600):
        board.alpha_composite(Image.open(CONTENT/'UI/Environment/Panel.png'),(35,y))
    for i, syllable in enumerate(['LAC','TE','OS','COM','PRAR','LIS','TA']):
        x,y = 920-(i%4)*300,2320-(i//4)*300
        board.alpha_composite(cell(border=8),(x,y))
        text(board,syllable,(x+110,y+110),54,bold=True)
    revealed = board.copy()
    ImageDraw.Draw(revealed).rectangle((60,230,660,350),fill='white')
    ImageDraw.Draw(revealed).rectangle((0,0,800,170),fill='white')
    text(revealed, 'PUNTOS: -5', (270,80), 60)
    text(revealed, 'SUPERMERCADO', (340,430), 62)
    success = revealed.copy()
    success.alpha_composite(Image.open(CONTENT/'UI/Environment/Excelente.png'),(65,1500))
    for name,im in [('menu',menu),('gameplay',board),('hint',revealed),('success',success),('tutorial',tutorial(4))]:
        im.thumbnail((480,1066))
        im.save(out / f'{name}.png')

if __name__ == '__main__':
    generate()
    preview()
