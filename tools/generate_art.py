"""Build the En Silabas light puzzle / colored tiles visual system from geometry."""
from pathlib import Path
import stat
from PIL import Image, ImageDraw, ImageFont
ROOT = Path(__file__).resolve().parents[1] / 'BeatEngine'
CONTENT = ROOT / 'BeatEngine.Core/Content'
BG='#F5F2EB'
SURFACE='#FFFFFF'
LINE='#D0D2C8'
INK='#242824'
PAPER='#FFF8E9'
MUTED='#636A63'
TEAL='#536F50'
YELLOW='#F6CF65'
MINT='#C7E7CC'
GRAY='#D4D8DB'
TILE_COLORS=(YELLOW,MINT,PAPER,GRAY)
FONT=CONTENT/'Fonts/AvenirLTStd-Medium.otf'
BOLD=CONTENT/'Fonts/Roboto-Bold.ttf'
SYLLABLE_FONT=CONTENT/'Fonts/RobotoMono-Bold.ttf'

def canvas(size, opaque=False):
    return Image.new('RGBA',size,BG if opaque else (0,0,0,0))

def text(im,value,xy,size=48,fill=INK,bold=False,syllable=False):
    font=SYLLABLE_FONT if syllable else BOLD if bold else FONT
    ImageDraw.Draw(im).text(xy,value,font=ImageFont.truetype(str(font),size),fill=fill,anchor='mm')

def cell(size=(220,220),border=3,face=PAPER):
    im=canvas(size)
    ImageDraw.Draw(im).rounded_rectangle((3,3,size[0]-4,size[1]-4),radius=13,fill=face,outline='#B9BEB5',width=3)
    return im

def button(size,label,primary=False):
    im=canvas(size);d=ImageDraw.Draw(im)
    d.rounded_rectangle((2,2,size[0]-3,size[1]-3),radius=5,fill=INK if primary else SURFACE,outline=INK if primary else LINE,width=3)
    text(im,label,(size[0]/2,size[1]/2),min(48,int(size[1]*.30)),SURFACE if primary else INK,bold=True)
    return im

def mark(size,opaque=True):
    im=canvas(size,opaque);d=ImageDraw.Draw(im);w,h=size
    unit=min(w,h)*.18
    for i in range(3):
        x=w/2+(i-1)*unit*1.15
        y=h/2+(1-i)*unit*.25
        d.rounded_rectangle((x-unit*.45,y-unit*.65,x+unit*.45,y+unit*.65),radius=1,fill=(YELLOW,TEAL,MINT)[i])
    return im

def tile_title(side=220,gap=20):
    im=canvas((side*4+gap*3,side))
    for i,syllable in enumerate(('EN','SI','LA','BAS')):
        x=i*(side+gap)
        im.alpha_composite(cell((side,side),face=TILE_COLORS[i]),(x,0))
        text(im,syllable,(x+side/2,side/2),int(side*.27),INK,syllable=True)
    return im


def title(size):
    im=canvas(size)
    logo=tile_title()
    im.alpha_composite(logo,((size[0]-logo.width)//2,210))
    text(im,'Cada cosa en su lugar',(size[0]/2,515),36,MUTED)
    return im

def tutorial(n):
    im=canvas((1080,1040));d=ImageDraw.Draw(im)
    d.rounded_rectangle((1,1,1078,1038),radius=4,fill=SURFACE,outline=LINE,width=2)
    headings=['Cada palabra empieza aqu\u00ed.','Una s\u00edlaba por casilla.','Ordena. Une. Descubre.','A tu ritmo.']
    lines=[('Forma tres palabras con las s\u00edlabas.','Mostrar Pista revela una ayuda.'),('Arrastra cada ficha a una fila.','Puedes moverla otra vez.'),('Lee cada fila de izquierda a derecha.','Completa las tres palabras.'),('Un intento incorrecto resta 400 puntos.','Mostrar Pista: -5. Saltar una ronda: -10.')]
    text(im,f'C\u00d3MO JUGAR   /   0{n}',(540,85),28,TEAL,bold=True)
    text(im,headings[n-1],(540,220),48,bold=True)
    for k,syllable in enumerate(['CA','MI','NO']):
        im.alpha_composite(cell(face=(YELLOW,GRAY,MINT)[k]),(180+k*240,380));text(im,syllable,(290+k*240,485),54,INK,syllable=True)
    for j,line in enumerate(lines[n-1]):text(im,line,(540,740+j*65),34,MUTED)
    for k in range(4):d.rounded_rectangle((456+k*48,940,484+k*48,946),radius=3,fill=TEAL if k==n-1 else LINE)
    return im

def panel():
    im=canvas((1125,225));d=ImageDraw.Draw(im)
    d.rounded_rectangle((2,2,1122,222),radius=28,fill='#E6E9DF',outline='#BCC5B5',width=3)
    # One continuous word tray, with subtle placement guides along its baseline.
    for x in range(0,1125,225):
        d.rounded_rectangle((x+58,184,x+167,188),radius=2,fill='#B5C0AC')
    return im

def feedback(label):
    mistake = label == "Puedes hacerlo mejor!"
    im=canvas((1080,360));d=ImageDraw.Draw(im)
    d.rounded_rectangle((90,35,990,325),radius=5,fill=SURFACE,outline=LINE,width=3)
    if mistake:
        d.arc((495,65,585,155),25,315,fill='#A76B36',width=8)
        d.polygon(((578,65),(598,83),(572,91)),fill='#A76B36')
    else:
        d.line((480,100,520,140,590,70),fill=TEAL,width=9)
    text(im,label,(540,215),60,bold=True)
    return im

def background():
    im=canvas((1200,2664),True);d=ImageDraw.Draw(im)
    im.alpha_composite(tile_title(100,12),(60,42))
    d.line((60,190,1140,190),fill=LINE,width=2)
    d.line((60,2550,1140,2550),fill=LINE,width=2)
    text(im,'UNA PALABRA A LA VEZ',(600,2600),24,MUTED)
    return im

def generate():
    paths=[p for p in ROOT.rglob('*') if p.suffix.lower() in ('.png','.jpg','.bmp','.ico') and not any(x in p.parts for x in ('bin','obj','ArtPreview'))]
    labels={'Start':'Nueva partida','Continue':'Continuar','Entendido':'Siguiente','Exit':'Salir','Back':'Volver','Pause':'Pausa','Play':'Jugar','Retry':'Reintentar'}
    for p in paths:
        with Image.open(p) as original:size=original.size
        name=p.stem
        if 'Backgrounds' in str(p):im=background() if name.endswith('_0') else canvas((1200,2664))
        elif name.startswith('Tutorial'):im=tutorial(int(name[-1]))
        elif p.parent.name=='Tiles':
            im=cell(face={'magenta':YELLOW,'blue':MINT,'leaf':PAPER,'gray':GRAY}.get(name,PAPER))
            if name=='front':text(im,'\u00b7',(110,105),64,INK)
        elif name.startswith('SylabrixTitle'):im=title((1080,620))
        elif name=='Welcome_button':
            im=canvas((1080,180))
        elif name=='ShowHint_button':im=button((600,120),'Mostrar Pista')
        elif name=='Arrow':im=button((220,110),'Saltar >')
        elif p.parent.name=='Buttons':im=button((1080,180),labels.get(name.split('_')[0],'Continuar'),name.startswith(('Start','Entendido','Play')))
        elif name=='Panel':im=panel()
        elif p.parent.name in ('Environment','Overlays'):
            label={'TryAgain':'Puedes hacerlo mejor!','Excelente':'Bien hecho.','GetReady':'Prepara tus palabras.','GetReady2':'Prepara tus palabras.','WatchCat':'Observa.','RepeatCat':'Tu turno.','you_win':'Bien hecho.','you_lose':'Int\u00e9ntalo otra vez.'}.get(name,'En Silabas')
            im=feedback(label)
        elif name=='Star':
            im=canvas((416,32));d=ImageDraw.Draw(im)
            for i in range(13):d.line((i*32+8,16,i*32+14,22,i*32+25,9),fill=TEAL,width=2)
        elif name in ('Splash','Default'):
            im=canvas(size,True);logo=mark((min(size)//2,)*2);im.alpha_composite(logo,((size[0]-logo.width)//2,(size[1]-logo.height)//2))
        else:im=mark(size)
        if p.suffix.lower() in ('.jpg','.bmp'):im=im.convert('RGB')
        p.chmod(p.stat().st_mode|stat.S_IWRITE)
        tmp=p.with_name(p.stem+'.generated'+p.suffix)
        if p.suffix.lower()=='.ico':im.save(tmp,sizes=[(16,16),(32,32),(48,48),(128,128),(256,256)])
        else:im.save(tmp)
        tmp.replace(p)
    print(f'Rebuilt {len(paths)} light puzzle / colored tiles assets.')

def preview():
    out=ROOT/'ArtPreview';out.mkdir(exist_ok=True)
    menu=background()
    for name,xy in [('Welcome_button',(60,220)),('SylabrixTitle',(60,480)),('Start_button',(60,1250)),('Continue_button',(60,1480))]:menu.alpha_composite(Image.open(CONTENT/f'UI/Buttons/{name}.png'),xy)
    board=background();text(board,'PUNTOS',(850,250),28,MUTED);text(board,'0',(850,305),48,bold=True)
    board.alpha_composite(button((600,120),'Mostrar Pista'),(60,230));board.alpha_composite(button((220,110),'Saltar >'),(900,30))
    text(board,'FORMA TRES PALABRAS',(360,465),30,TEAL,bold=True)
    text(board,'S\u00cdLABAS DISPONIBLES',(340,1310),30,MUTED,bold=True)
    for y in (1000,760,520):board.alpha_composite(panel(),(35,y))
    for i,syllable in enumerate(['LAC','TE','OS','COM','PRAR','LIS','TA']):
        x,y=910-(i%4)*280,1610-(i//4)*240
        board.alpha_composite(cell(face=TILE_COLORS[sum(map(ord,syllable))%4]),(x,y));text(board,syllable,(x+110,y+105),54,INK,syllable=True)
    revealed=board.copy();d=ImageDraw.Draw(revealed);revealed.paste(background().crop((60,230,661,351)),(60,230));revealed.paste(background().crop((700,275,1000,350)),(700,275))
    text(revealed,'-5',(850,305),48,bold=True);text(revealed,'Pista: SUPERMERCADO',(390,395),40,TEAL)
    success=background()
    for y in (1000,760,520):success.alpha_composite(panel(),(35,y))
    success.alpha_composite(feedback('Bien hecho.'),(65,1500))
    for name,im in [('menu',menu),('gameplay',board),('hint',revealed),('success',success),('tutorial',tutorial(4))]:
        im.thumbnail((480,1066));temporary=out/f'{name}.new.png';im.save(temporary);temporary.replace(out/f'{name}.png')
if __name__=='__main__':generate();preview()
