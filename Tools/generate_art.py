#!/usr/bin/env python3
"""Build the original, high-resolution Kor Frontier raster art set.

The artwork is procedurally painted from hand-authored silhouettes, layered
lighting, material noise, weathering, and markings. No third-party or C&C art
is used. Re-run from the project directory after editing the palette/shapes.
"""
from pathlib import Path
import math, random
from PIL import Image, ImageDraw, ImageFilter, ImageChops

OUT = Path(__file__).resolve().parents[1] / "Assets" / "Art"
OUT.mkdir(parents=True, exist_ok=True)
random.seed(240318)

def texture(size, base, amplitude=10, seed=1):
    rng = random.Random(seed)
    noise = Image.effect_noise(size, 38).convert("L")
    # Very subtle large-scale clouding over finer grain.
    cloud = Image.effect_noise((max(2,size[0]//12), max(2,size[1]//12)), 72).resize(size, Image.Resampling.BICUBIC).filter(ImageFilter.GaussianBlur(max(size)/100))
    pix = Image.new("RGBA", size)
    n, c = noise.load(), cloud.load()
    p = pix.load()
    for y in range(size[1]):
        for x in range(size[0]):
            # Keep texture readable after sprites are downsampled into the 16:9 view.
            v = (n[x,y]-128)*amplitude/150 + (c[x,y]-128)*amplitude/220
            p[x,y] = tuple(max(0,min(255,int(k+v))) for k in base)+(255,)
    return pix

def overlay_grain(im, amount=22, seed=1):
    rng = random.Random(seed)
    layer = Image.new("RGBA", im.size, (0,0,0,0)); d=ImageDraw.Draw(layer)
    for _ in range(im.width*im.height//210):
        x=rng.randrange(im.width); y=rng.randrange(im.height)
        a=rng.randrange(5,amount)
        col=(238,220,184,a) if rng.random()<.52 else (2,5,7,a)
        d.point((x,y),fill=col)
    return Image.alpha_composite(im.convert("RGBA"),layer)

def masked_material(size, base, poly, seed, amp=14):
    mat=texture(size,base,amp,seed)
    mask=Image.new("L",size,0); ImageDraw.Draw(mask).polygon(poly,fill=255)
    mat.putalpha(mask); return mat

def bevel_polygon(d, pts, dark=(16,20,19,255), light=(173,168,143,255), width=3):
    d.line(pts+[pts[0]],fill=dark,width=width,joint="curve")
    # Top/left edge receives a warm industrial highlight.
    d.line(pts[:max(2,len(pts)//2+1)],fill=light,width=max(1,width//2),joint="curve")

def make_menu():
    w,h=2560,1440
    im=Image.new("RGB",(w,h)); px=im.load()
    for y in range(h):
        t=y/h
        col=(int(8+22*t),int(14+10*t),int(19+5*t))
        for x in range(w): px[x,y]=col
    im=im.convert("RGBA")
    haze=Image.new("RGBA",(w,h),(0,0,0,0)); hd=ImageDraw.Draw(haze)
    hd.ellipse((190,120,1460,1320),fill=(188,53,34,56)); haze=haze.filter(ImageFilter.GaussianBlur(100)); im=Image.alpha_composite(im,haze)
    # Distant ridgeline layers.
    d=ImageDraw.Draw(im)
    for layer in range(4):
        base=900+layer*65; col=(14+layer*4,20+layer*3,23+layer*2,255)
        pts=[(0,h)]; x=0
        while x<=w:
            y=base+random.randrange(-85,100); pts.append((x,y)); x+=random.randrange(75,180)
        pts += [(w,h)]; d.polygon(pts,fill=col)
    # Foundry district: asymmetric towers, gantries, silos, stacks, and amber windows.
    ground=1110
    for i in range(36):
        x=i*75+random.randrange(-22,27); bw=random.randrange(48,156); bh=random.randrange(180,520)
        top=ground-bh; shade=random.randrange(15,34)
        body=texture((bw,bh),(shade,shade+5,shade+5),19,100+i)
        im.alpha_composite(body,(x,top))
        d=ImageDraw.Draw(im)
        d.rectangle((x,top,x+bw,ground),fill=(0,0,0,0),outline=(94,93,80,155),width=2)
        d.polygon([(x,top+20),(x+14,top),(x+bw-12,top),(x+bw,top+16),(x+bw,top+42),(x,top+42)],fill=(44,48,45,255))
        d.line((x+5,top+46,x+bw-4,top+46),fill=(120,78,57,100),width=2)
        # Strong perspective-facing wall.
        d.polygon([(x+bw*.78,top+37),(x+bw,top+18),(x+bw,ground),(x+bw*.78,ground)],fill=(8,13,16,110))
        cols=max(1,int(bw/25))
        for row in range(max(2,bh//43)):
            for col in range(cols):
                wx=x+10+col*22; wy=top+65+row*39
                if wy<ground-12 and random.random()<.57:
                    d.rectangle((wx,wy,wx+7,wy+16),fill=(201,111,57,random.randrange(50,155)))
                    d.line((wx+1,wy+2,wx+6,wy+2),fill=(255,195,103,110),width=1)
        # weathered facade ribs, ventilation doors, warning paint.
        for xx in range(x+6,x+bw,24): d.line((xx,top+50,xx,ground),fill=(120,129,119,35),width=1)
        d.rectangle((x,ground-9,x+bw,ground),fill=(7,10,11,255))
    d=ImageDraw.Draw(im)
    # Distant cranes and transmission gantries.
    for x,y,scale in [(280,585,1.2),(850,465,1.5),(1510,560,1.1),(2080,500,1.4)]:
        col=(100,89,69,185); d.line((x,y,x,y+410*scale),fill=col,width=int(5*scale)); d.line((x-240*scale,y+12,x+190*scale,y+12),fill=col,width=int(5*scale));
        d.line((x-180*scale,y+14,x-180*scale,y+170*scale),fill=col,width=3); d.line((x-190*scale,y+170*scale,x-170*scale,y+170*scale),fill=col,width=3)
        for dx in range(-220,180,35): d.line((x+dx*scale,y+16,x+(dx+70)*scale,y+52),fill=(102,93,74,95),width=2)
    # Smelter glow and exhaust clouds.
    glow=Image.new("RGBA",(w,h),(0,0,0,0)); gd=ImageDraw.Draw(glow)
    gd.ellipse((650,680,1950,1320),fill=(204,79,35,30)); glow=glow.filter(ImageFilter.GaussianBlur(100)); im=Image.alpha_composite(im,glow)
    smoke=Image.new("RGBA",(w,h),(0,0,0,0)); sd=ImageDraw.Draw(smoke)
    for i in range(55):
        x=random.randrange(w); y=random.randrange(200,850); r=random.randrange(35,150)
        sd.ellipse((x-r,y-r*.55,x+r,y+r*.55),fill=(103,103,93,random.randrange(3,15)))
    smoke=smoke.filter(ImageFilter.GaussianBlur(50)); im=Image.alpha_composite(im,smoke)
    # Cinematic vignette and grain.
    vign=Image.new("L",(w,h),0); vp=vign.load()
    for y in range(h):
        for x in range(w):
            dx=(x-w/2)/(w/2); dy=(y-h/2)/(h/2); vp[x,y]=int(max(0,min(205,(dx*dx+dy*dy)*70)))
    shade=Image.new("RGBA",(w,h),(0,0,0,0)); shade.putalpha(vign); im=Image.alpha_composite(im,shade)
    im=overlay_grain(im,12,63).convert("RGB"); im.save(OUT/"menu-foundry-2560.png",quality=96)

def make_tile(index, infected=False):
    w,h=256,128; cx,cy=w//2,h//2; pts=[(cx,3),(w-3,cy),(cx,h-3),(3,cy)]
    base=(60+index*2,60+index,49+index%3)
    im=texture((w,h),base,24,index+1).convert("RGBA")
    mask=Image.new("L",(w,h),0); ImageDraw.Draw(mask).polygon(pts,fill=255)
    im.putalpha(mask); d=ImageDraw.Draw(im)
    d.line(pts+[pts[0]],fill=(123,127,101,180),width=3,joint="curve")
    d.line((cx,7,w-7,cy),fill=(181,163,126,35),width=2)
    # strata, scuffs, loose stone, and dry scrub are clipped to the diamond.
    rng=random.Random(100+index)
    for j in range(55):
        x=rng.randrange(15,w-15); y=rng.randrange(10,h-10)
        if mask.getpixel((x,y)):
            r=rng.choice([1,2,3,4]); col=(189,170,128,rng.randrange(20,95)) if j%3 else (11,18,16,rng.randrange(25,90))
            d.ellipse((x-r,y-r//2,x+r,y+r//2+1),fill=col)
    for _ in range(5):
        x=rng.randrange(32,w-32); y=rng.randrange(18,h-18)
        d.line((x,y,x+rng.randrange(-24,25),y+rng.randrange(-6,7)),fill=(202,187,142,50),width=1)
    if infected:
        tint=Image.new("RGBA",(w,h),(98,5,26,90)); tint.putalpha(ImageChops.multiply(tint.getchannel("A"), mask)); im=Image.alpha_composite(im,tint)
        d=ImageDraw.Draw(im)
        for j in range(7):
            x=rng.randrange(55,205); y=rng.randrange(30,100); r=rng.randrange(7,20)
            d.ellipse((x-r*1.4,y-r,x+r*1.4,y+r),fill=(100+rng.randrange(40),12,28,rng.randrange(110,200)),outline=(226,65,60,150),width=2)
            for k in range(3):
                ex=x+rng.randrange(-32,33); ey=y+rng.randrange(-28,29)
                d.line((x,y,ex,ey),fill=(226,70,65,140),width=2)
    return im

def make_worker():
    w,h=256,320; im=Image.new("RGBA",(w,h),(0,0,0,0))
    shadow=Image.new("RGBA",(w,h),(0,0,0,0)); ImageDraw.Draw(shadow).ellipse((45,258,215,300),fill=(0,0,0,155)); shadow=shadow.filter(ImageFilter.GaussianBlur(12)); im=Image.alpha_composite(im,shadow)
    # Backpack silhouette and depth.
    d=ImageDraw.Draw(im)
    d.rounded_rectangle((154,108,207,239),radius=17,fill=(38,44,40,255),outline=(128,128,107,255),width=4)
    d.rectangle((165,125,199,137),fill=(106,90,61,255)); d.line((166,142,198,142),fill=(202,161,86,255),width=5)
    d.rounded_rectangle((169,151,197,224),radius=9,fill=(27,32,31,255),outline=(80,86,76,255),width=3)
    # Boots / legs, with steel caps.
    d.polygon([(88,213),(128,215),(123,265),(107,277),(75,271)],fill=(38,44,42,255),outline=(122,126,108,255))
    d.polygon([(128,215),(168,212),(180,267),(160,279),(132,265)],fill=(49,52,46,255),outline=(154,146,119,255))
    d.rounded_rectangle((70,262,126,292),radius=7,fill=(20,24,23,255),outline=(119,119,103,255),width=4)
    d.rounded_rectangle((137,261,193,292),radius=7,fill=(22,26,25,255),outline=(131,128,109,255),width=4)
    for x in range(78,124,12): d.line((x,286,x+7,286),fill=(165,150,109,170),width=2)
    for x in range(145,190,12): d.line((x,286,x+7,286),fill=(165,150,109,170),width=2)
    # Armored coat with tailored panels.
    torso=[(82,112),(111,95),(153,102),(181,128),(171,216),(145,239),(99,231),(76,205)]
    im.alpha_composite(masked_material((w,h),(73,78,69),torso,5,20)); d=ImageDraw.Draw(im)
    d.line(torso+[torso[0]],fill=(172,166,140,255),width=5,joint="curve")
    d.polygon([(109,111),(150,112),(160,170),(103,174)],fill=(45,51,48,255),outline=(126,130,111,255))
    d.polygon([(111,118),(146,118),(151,131),(109,131)],fill=(112,48,45,255))
    d.rectangle((111,137,147,143),fill=(206,159,83,255)); d.rectangle((117,146,141,154),fill=(52,59,55,255))
    d.line((111,166,150,166),fill=(172,162,130,210),width=3); d.line((105,175,150,175),fill=(32,39,37,220),width=3)
    # Shoulder plates and gloves.
    d.polygon([(69,124),(82,106),(105,112),(107,137),(91,153),(71,146)],fill=(105,58,48,255),outline=(213,167,93,255),width=3)
    d.polygon([(158,110),(180,117),(194,139),(182,154),(162,145)],fill=(125,64,51,255),outline=(211,170,96,255),width=3)
    d.polygon([(72,146),(94,155),(87,201),(67,211),(54,198)],fill=(57,64,57,255),outline=(145,144,122,255),width=3)
    d.polygon([(178,150),(197,145),(212,190),(198,208),(177,193)],fill=(53,59,55,255),outline=(145,144,122,255),width=3)
    d.ellipse((48,192,78,221),fill=(35,40,37,255),outline=(174,159,123,255),width=3)
    d.ellipse((191,192,219,222),fill=(34,40,37,255),outline=(174,159,123,255),width=3)
    # Neck, helmet and visor.
    d.rectangle((112,87,151,116),fill=(48,53,48,255))
    d.ellipse((91,27,172,111),fill=(194,157,83,255))
    d.pieslice((82,18,180,116),180,360,fill=(45,50,48,255),outline=(207,187,139,255),width=5)
    d.rectangle((83,64,178,87),fill=(35,41,40,255)); d.rounded_rectangle((105,66,157,77),radius=4,fill=(229,174,80,255))
    d.line((93,48,166,48),fill=(125,129,111,255),width=4); d.line((98,89,164,89),fill=(163,155,128,255),width=4)
    # Harness and tools.
    d.line((105,112,148,203),fill=(212,169,88,255),width=7); d.line((159,116,123,204),fill=(30,35,34,255),width=6)
    d.rounded_rectangle((91,186,119,208),radius=4,fill=(24,29,28,255),outline=(190,154,86,255),width=3)
    d.line((203,218,225,173),fill=(177,181,159,255),width=6); d.line((215,181,235,166),fill=(209,175,100,255),width=5)
    # Worn edge nicks, bolts, cloth seams and dust.
    for x,y in [(83,133),(99,118),(168,134),(86,205),(169,210),(121,181),(144,188),(76,149),(184,166)]:
        d.ellipse((x-3,y-3,x+3,y+3),fill=(213,183,117,255))
    rng=random.Random(3)
    for _ in range(370):
        x=rng.randrange(56,213); y=rng.randrange(98,279)
        if im.getpixel((x,y))[3]>100 and rng.random()<.45:
            d.point((x,y),fill=(197,181,144,rng.randrange(30,125)))
    return im

def make_guardian():
    im=make_worker(); im=im.resize((288,336),Image.Resampling.LANCZOS)
    d=ImageDraw.Draw(im)
    # Add powered exoskeleton shoulder mantle, chest plates, reactor and carbine.
    d.polygon([(62,118),(80,91),(123,95),(135,124),(123,154),(78,149)],fill=(101,47,49,255),outline=(220,176,99,255),width=5)
    d.polygon([(167,99),(209,104),(236,133),(224,160),(183,150)],fill=(88,43,46,255),outline=(219,175,97,255),width=5)
    d.polygon([(91,132),(181,124),(193,205),(168,237),(98,224)],fill=(99,105,96,255),outline=(212,193,147,255),width=5)
    d.polygon([(111,141),(169,138),(179,175),(105,177)],fill=(45,50,47,255),outline=(143,150,131,255),width=3)
    d.rounded_rectangle((127,145,161,169),radius=5,fill=(164,47,48,255),outline=(234,190,102,255),width=3)
    d.rectangle((137,151,153,158),fill=(251,194,89,255))
    # Armored thigh panels and articulated joints.
    for box in [(82,207,123,270),(162,207,205,270)]: d.rounded_rectangle(box,radius=10,fill=(53,61,58,255),outline=(165,167,142,255),width=4)
    for x in (99,185):
        d.line((x,221,x,257),fill=(142,48,48,255),width=6)
        for y in (224,242,258): d.ellipse((x-5,y-5,x+5,y+5),fill=(215,173,97,255))
    # Heavy carbine, reinforced receiver and muzzle.
    d.line((180,159,252,102),fill=(35,41,40,255),width=15);d.line((184,156,250,103),fill=(186,184,162,255),width=4)
    d.polygon([(239,95),(265,80),(276,91),(258,111)],fill=(60,66,61,255),outline=(208,174,103,255),width=3)
    d.ellipse((263,77,280,94),fill=(204,52,50,255))
    return overlay_grain(im,18,31)

def make_tank():
    w,h=448,320; im=Image.new("RGBA",(w,h),(0,0,0,0))
    sh=Image.new("RGBA",(w,h),(0,0,0,0)); ImageDraw.Draw(sh).ellipse((30,194,425,297),fill=(0,0,0,185)); sh=sh.filter(ImageFilter.GaussianBlur(15)); im=Image.alpha_composite(im,sh)
    d=ImageDraw.Draw(im)
    # Track runs with individual road wheels and thick segmented skirts.
    for x0 in (48,251):
        d.rounded_rectangle((x0,169,x0+150,262),radius=34,fill=(21,26,26,255),outline=(147,143,121,255),width=5)
        d.rounded_rectangle((x0+9,179,x0+141,251),radius=28,fill=(55,59,53,255),outline=(84,89,78,255),width=4)
        for x in range(x0+20,x0+137,25):
            d.ellipse((x-11,193,x+11,238),fill=(29,34,33,255),outline=(166,157,125,255),width=3)
            d.ellipse((x-5,205,x+5,225),fill=(105,107,94,255))
        for y in range(177,256,12): d.line((x0+8,y,x0+142,y+3),fill=(183,169,127,100),width=2)
    # Lower hull and layered angular armor plates.
    lower=[(37,153),(102,119),(337,121),(414,171),(398,229),(354,255),(79,251),(35,218)]
    im.alpha_composite(masked_material((w,h),(82,88,75),lower,21,24)); d=ImageDraw.Draw(im); bevel_polygon(d,lower,(19,25,24,255),(192,178,132,255),6)
    d.polygon([(38,155),(102,120),(335,122),(412,169),(358,183),(88,181)],fill=(133,129,104,255),outline=(211,191,143,255),width=4)
    d.polygon([(87,182),(358,183),(399,220),(350,249),(86,244),(42,214)],fill=(53,62,57,255),outline=(115,121,104,255),width=4)
    # Hull glacis, segmented composite plates and chipped red identification stripes.
    d.polygon([(81,156),(128,134),(318,136),(371,165),(327,186),(115,184)],fill=(112,116,99,255),outline=(195,181,139,255),width=4)
    for x in (110,164,222,281,334):
        d.line((x,147,x+18,177),fill=(46,53,49,255),width=4);d.ellipse((x+8,151,x+14,157),fill=(220,181,104,255))
    d.polygon([(100,166),(161,166),(152,177),(91,177)],fill=(151,43,46,255)); d.polygon([(298,166),(350,166),(361,175),(303,176)],fill=(151,43,46,255))
    # Turret, mantlet, sensor suite, hatch and heavy cannon.
    turret=[(150,117),(180,77),(281,73),(334,106),(327,149),(179,152)]
    im.alpha_composite(masked_material((w,h),(78,85,75),turret,25,20));d=ImageDraw.Draw(im);bevel_polygon(d,turret,(20,25,24,255),(198,184,136,255),5)
    d.polygon([(175,113),(200,85),(274,81),(319,105),(306,124),(180,129)],fill=(139,133,104,255),outline=(207,191,145,255),width=4)
    d.polygon([(180,129),(306,124),(327,143),(310,157),(181,156)],fill=(70,78,69,255))
    d.rounded_rectangle((219,83,264,107),radius=7,fill=(33,40,39,255),outline=(183,174,139,255),width=4)
    d.ellipse((229,88,253,101),fill=(207,166,88,255)); d.ellipse((236,91,247,98),fill=(242,197,101,255))
    d.line((305,110,394,76),fill=(50,56,52,255),width=27); d.line((323,105,397,76),fill=(191,183,149,255),width=5)
    d.polygon([(384,66),(437,52),(444,65),(401,87)],fill=(36,42,41,255),outline=(182,174,139,255),width=4)
    d.ellipse((428,47,447,67),fill=(185,47,44,255))
    # Reactive armor blocks, service panels, serial stencil, antennae and battle damage.
    for x,y in [(114,140),(145,137),(286,140),(316,145),(109,205),(341,208)]:
        d.rounded_rectangle((x,y,x+24,y+17),radius=3,fill=(75,81,70,255),outline=(186,171,125,255),width=2)
    d.rectangle((180,133,222,140),fill=(160,44,46,255)); d.rectangle((187,135,195,138),fill=(237,189,94,255))
    d.line((184,82,171,40),fill=(123,129,116,255),width=4); d.ellipse((165,32,177,44),fill=(192,44,46,255))
    d.line((281,85,291,37),fill=(112,119,108,255),width=3); d.ellipse((286,29,296,39),fill=(210,171,91,255))
    rng=random.Random(29)
    for _ in range(95):
        x=rng.randrange(55,400); y=rng.randrange(90,242)
        if im.getpixel((x,y))[3] and rng.random()<.62:
            d.line((x,y,x+rng.randrange(2,11),y+rng.randrange(-2,3)),fill=(216,203,162,rng.randrange(55,160)),width=1)
    return overlay_grain(im,15,22).resize((896,640),Image.Resampling.LANCZOS)

def make_base():
    w,h=560,400; im=Image.new("RGBA",(w,h),(0,0,0,0)); d=ImageDraw.Draw(im)
    sh=Image.new("RGBA",(w,h),(0,0,0,0)); ImageDraw.Draw(sh).ellipse((20,291,540,378),fill=(0,0,0,185)); sh=sh.filter(ImageFilter.GaussianBlur(20)); im=Image.alpha_composite(im,sh); d=ImageDraw.Draw(im)
    # Base armored habitat / command post: layered prefab modules and roof girders.
    d.polygon([(59,173),(124,96),(360,84),(478,154),(507,291),(435,338),(108,325),(48,276)],fill=(40,47,44,255),outline=(190,183,149,255),width=6)
    d.polygon([(69,175),(135,111),(354,100),(453,159),(426,193),(111,202)],fill=(127,126,104,255),outline=(215,198,157,255),width=5)
    d.polygon([(111,202),(426,193),(489,269),(435,322),(110,306),(58,265)],fill=(52,61,57,255),outline=(131,137,120,255),width=5)
    # Roof segmentation, solar protective fins, turret cap.
    for x in (112,183,257,330,396):
        d.polygon([(x,142),(x+16,118),(x+70,117),(x+84,147),(x+73,158),(x+21,158)],fill=(80,87,76,255),outline=(189,179,145,255),width=3)
        d.line((x+24,125,x+66,125),fill=(230,184,97,255),width=3)
    d.rectangle((122,176,422,186),fill=(151,43,47,255));d.rectangle((142,178,171,184),fill=(233,184,89,255));d.rectangle((374,178,403,184),fill=(233,184,89,255))
    # Habitation windows, reinforced gate and flight of lights.
    for x in (95,427):
        d.rectangle((x,224,x+46,272),fill=(23,30,30,255),outline=(181,179,150,255),width=4)
        d.rectangle((x+8,231,x+38,254),fill=(177,132,70,255));d.rectangle((x+12,235,x+34,251),fill=(236,182,87,255))
        d.line((x+22,225,x+22,270),fill=(81,87,77,255),width=3)
    d.rounded_rectangle((218,207,338,312),radius=12,fill=(21,28,29,255),outline=(169,177,158,255),width=6)
    d.rectangle((236,222,321,238),fill=(154,42,46,255));d.rectangle((246,228,311,233),fill=(232,179,83,255))
    d.rectangle((239,250,318,300),fill=(36,43,43,255));d.line((278,253,278,300),fill=(123,132,118,255),width=4)
    # Kor mark / beacon, vents, antenna and service ladders.
    d.ellipse((255,143,302,190),fill=(212,168,83,255),outline=(247,218,152,255),width=4)
    d.polygon([(278,150),(288,168),(278,184),(268,168)],fill=(58,61,51,255))
    d.line((475,168,490,74),fill=(178,177,150,255),width=5);d.line((457,105,499,105),fill=(178,177,150,255),width=3)
    d.ellipse((483,63,500,80),fill=(199,47,44,255));
    for y in range(216,298,13): d.line((447,y,466,y-5),fill=(196,171,113,255),width=3)
    for x in range(137,411,23): d.line((x,285,x+12,285),fill=(206,170,91,155),width=3)
    return overlay_grain(im,13,47)

def make_purifier():
    w,h=320,320; im=Image.new("RGBA",(w,h),(0,0,0,0));d=ImageDraw.Draw(im)
    sh=Image.new("RGBA",(w,h),(0,0,0,0));ImageDraw.Draw(sh).ellipse((27,258,293,306),fill=(0,0,0,155));sh=sh.filter(ImageFilter.GaussianBlur(14));im=Image.alpha_composite(im,sh);d=ImageDraw.Draw(im)
    d.polygon([(54,250),(76,117),(117,73),(207,77),(250,123),(276,251),(228,275),(87,270)],fill=(70,82,74,255),outline=(206,190,148,255),width=5)
    d.polygon([(87,122),(119,88),(203,91),(234,127),(216,144),(102,145)],fill=(129,129,102,255),outline=(214,195,145,255),width=4)
    # Core, vent banks, hazard stripes and red glass.
    d.polygon([(130,101),(161,38),(190,103)],fill=(148,49,46,255),outline=(224,180,101,255),width=4)
    d.ellipse((146,75,174,103),fill=(244,195,97,255));d.ellipse((153,82,167,96),fill=(255,235,171,255))
    d.rectangle((104,153,219,243),fill=(25,33,32,255),outline=(164,173,151,255),width=4)
    for i in range(6):
        x=114+i*16;d.rounded_rectangle((x,165,x+8,223),radius=3,fill=(103,121,105,255))
        d.line((x+3,169,x+3,219),fill=(197,186,139,180),width=2)
    d.rectangle((76,234,250,246),fill=(152,43,46,255));d.rectangle((91,236,111,244),fill=(233,183,91,255));d.rectangle((217,236,237,244),fill=(233,183,91,255))
    d.line((77,113,59,77),fill=(158,169,148,255),width=5);d.line((244,119,272,83),fill=(158,169,148,255),width=5)
    d.ellipse((48,65,69,85),fill=(192,48,47,255));d.ellipse((263,71,283,91),fill=(202,165,89,255))
    for x in (96,230):
        for y in (156,231):d.ellipse((x-4,y-4,x+4,y+4),fill=(219,180,107,255))
    return overlay_grain(im,16,41)

def make_blight():
    w,h=320,260; im=Image.new("RGBA",(w,h),(0,0,0,0)); sh=Image.new("RGBA",(w,h),(0,0,0,0));ImageDraw.Draw(sh).ellipse((22,202,298,249),fill=(0,0,0,185));sh=sh.filter(ImageFilter.GaussianBlur(12)); im=Image.alpha_composite(im,sh)
    # Toxic organic mass and hooked limbs; textured layers avoid clean icon geometry.
    rng=random.Random(881); d=ImageDraw.Draw(im)
    for i in range(14):
        x=rng.randrange(74,243);y=rng.randrange(74,203);rx=rng.randrange(17,52);ry=rng.randrange(13,43)
        col=(rng.randrange(53,111),rng.randrange(9,26),rng.randrange(23,42),rng.randrange(160,238))
        d.ellipse((x-rx,y-ry,x+rx,y+ry),fill=col,outline=(146,32,49,200),width=3)
    d=ImageDraw.Draw(im)
    tendrils=[[(104,166),(60,147),(50,114),(25,87)],[(115,139),(83,105),(77,76),(58,55)],[(213,158),(255,137),(254,101),(290,78)],[(205,133),(235,105),(224,72),(249,41)],[(148,104),(130,66),(146,40),(128,19)],[(183,108),(196,66),(183,39),(199,19)]]
    for pts in tendrils:
        d.line(pts,fill=(75,12,29,255),width=18,joint="curve");d.line(pts,fill=(184,43,57,255),width=7,joint="curve")
        x,y=pts[-1]; d.polygon([(x-11,y+4),(x+7,y-7),(x+2,y+11)],fill=(225,77,67,255))
    d.ellipse((89,78,231,211),fill=(78,16,36,255),outline=(198,44,61,255),width=7)
    d.ellipse((116,62,207,179),fill=(139,29,50,255),outline=(224,61,67,255),width=5)
    d.ellipse((138,81,190,149),fill=(210,54,60,255));d.ellipse((152,96,181,126),fill=(248,135,88,255));d.ellipse((158,100,172,114),fill=(255,211,143,255))
    d.arc((116,96,208,193),18,153,fill=(255,127,89,240),width=6)
    # Encrusted plates, vein cracks, and wet highlights.
    for x,y in [(107,123),(202,137),(123,172),(182,178),(164,74),(218,158)]:
        d.polygon([(x,y-12),(x+10,y-3),(x+6,y+9),(x-7,y+6)],fill=(48,12,28,255),outline=(203,61,67,230),width=2)
    for pts in [[(116,176),(141,157),(154,167),(174,149)],[(198,178),(183,158),(187,143)],[(132,94),(151,111),(142,132)]]: d.line(pts,fill=(255,107,78,200),width=3)
    return overlay_grain(im,23,79)

def make_ore():
    w=h=256; im=Image.new("RGBA",(w,h),(0,0,0,0));d=ImageDraw.Draw(im)
    sh=Image.new("RGBA",(w,h),(0,0,0,0));ImageDraw.Draw(sh).ellipse((28,193,230,233),fill=(0,0,0,145));sh=sh.filter(ImageFilter.GaussianBlur(12));im=Image.alpha_composite(im,sh);d=ImageDraw.Draw(im)
    crystals=[([(42,190),(58,104),(91,51),(126,137),(123,209)],(124,122,101,255)),([(96,202),(118,82),(161,28),(192,145),(182,216)],(146,136,103,255)),([(156,205),(182,116),(215,67),(238,170),(216,213)],(105,110,93,255)),([(30,205),(42,164),(68,139),(83,207)],(79,89,78,255))]
    for poly,col in crystals:
        d.polygon(poly,fill=col,outline=(205,189,143,255));
        a,b,c=poly[0],poly[1],poly[2]; d.polygon([a,b,c,((b[0]+c[0])//2,(b[1]+c[1])//2)],fill=(208,195,150,180))
        d.line((poly[1],poly[2],poly[3]),fill=(244,212,137,210),width=4)
    for x,y in [(82,166),(145,185),(204,181),(119,131),(55,190)]: d.ellipse((x-6,y-4,x+6,y+4),fill=(228,173,78,220))
    return overlay_grain(im,12,23)

def make_rocks():
    w,h=300,190; im=Image.new("RGBA",(w,h),(0,0,0,0)); sh=Image.new("RGBA",(w,h),(0,0,0,0))
    ImageDraw.Draw(sh).ellipse((22,136,278,178),fill=(0,0,0,165)); sh=sh.filter(ImageFilter.GaussianBlur(12)); im=Image.alpha_composite(im,sh)
    d=ImageDraw.Draw(im)
    for pts,base,seed in [
        ([(31,144),(46,103),(79,74),(116,91),(129,145),(102,159),(53,159)],(84,86,76),17),
        ([(95,148),(112,82),(154,40),(194,71),(215,142),(186,159),(125,160)],(99,99,86),19),
        ([(177,151),(198,101),(237,83),(272,119),(269,153),(218,165)],(73,80,73),21),
        ([(48,151),(61,126),(84,111),(104,145),(92,161)],(63,71,67),23)]:
        mat=masked_material((w,h),base,pts,seed,18); im.alpha_composite(mat); d=ImageDraw.Draw(im)
        d.line(pts+[pts[0]],fill=(164,157,131,255),width=3,joint="curve")
        # Fracture planes, mineral seams and broken chips catch the overhead light.
        cx=sum(x for x,y in pts)//len(pts); cy=sum(y for x,y in pts)//len(pts)
        d.polygon([pts[1],pts[2],(cx,cy),(cx-5,cy-10)],fill=(145,143,123,175))
        d.line((pts[1],pts[2],cx,cy,pts[-2]),fill=(194,178,135,160),width=2)
        for x,y in pts[1:-1]:d.ellipse((x-2,y-2,x+3,y+3),fill=(207,189,144,210))
    return overlay_grain(im,13,24)

def make_supply_cache():
    w,h=280,220; im=Image.new("RGBA",(w,h),(0,0,0,0)); d=ImageDraw.Draw(im)
    sh=Image.new("RGBA",(w,h),(0,0,0,0)); ImageDraw.Draw(sh).ellipse((15,173,267,213),fill=(0,0,0,155));sh=sh.filter(ImageFilter.GaussianBlur(12));im=Image.alpha_composite(im,sh);d=ImageDraw.Draw(im)
    d.polygon([(35,168),(69,136),(230,137),(256,170),(235,191),(49,191)],fill=(45,51,46,255),outline=(166,157,123,255),width=4)
    for x,y,wid,hei,col in [(40,100,86,73,(109,76,49,255)),(112,77,90,94,(69,76,67,255)),(177,107,67,65,(123,77,47,255)),(79,50,65,77,(83,85,69,255))]:
        d.polygon([(x,y+9),(x+12,y),(x+wid,y+4),(x+wid-4,y+hei),(x+6,y+hei+3)],fill=col,outline=(194,175,131,255),width=4)
        d.line((x+8,y+18,x+wid-8,y+20),fill=(41,45,40,255),width=4)
        for yy in range(y+28,y+hei,20): d.line((x+10,yy,x+wid-9,yy+2),fill=(219,181,104,150),width=2)
        d.ellipse((x+7,y+7,x+13,y+13),fill=(232,194,111,255));d.ellipse((x+wid-14,y+8,x+wid-8,y+14),fill=(232,194,111,255))
    d.polygon([(112,111),(203,112),(200,151),(115,150)],fill=(147,43,45,255),outline=(228,183,109,255),width=3)
    d.rectangle((133,121,181,140),fill=(210,171,94,255));d.line((137,130,177,130),fill=(65,60,45,255),width=3)
    return overlay_grain(im,17,223)

def make_engineer():
    """Distinct field engineer with a high visibility hood and service rig."""
    w,h=280,340; im=Image.new("RGBA",(w,h),(0,0,0,0))
    sh=Image.new("RGBA",(w,h),(0,0,0,0)); ImageDraw.Draw(sh).ellipse((38,286,239,322),fill=(0,0,0,155)); sh=sh.filter(ImageFilter.GaussianBlur(12)); im=Image.alpha_composite(im,sh)
    d=ImageDraw.Draw(im)
    # Tool frame, welding canister, hose and insulated apron distinguish the role.
    d.rounded_rectangle((166,99,225,254),radius=15,fill=(34,42,40,255),outline=(176,166,131,255),width=4)
    d.rounded_rectangle((180,114,215,178),radius=8,fill=(104,79,49,255),outline=(205,171,99,255),width=3)
    d.ellipse((189,121,206,138),fill=(218,164,81,255)); d.line((185,186,216,186),fill=(188,48,47,255),width=9)
    d.line((172,143,151,166,164,206,193,211),fill=(193,164,103,255),width=5,joint="curve")
    # Boots and reinforced legs.
    d.polygon([(91,224),(133,225),(125,282),(102,297),(71,286)],fill=(48,54,49,255),outline=(160,154,126,255))
    d.polygon([(134,225),(171,219),(190,280),(168,298),(139,275)],fill=(57,60,52,255),outline=(170,159,127,255))
    d.rounded_rectangle((66,281,126,307),radius=6,fill=(24,29,28,255),outline=(177,164,126,255),width=4)
    d.rounded_rectangle((151,281,208,307),radius=6,fill=(23,28,27,255),outline=(177,164,126,255),width=4)
    # Canvas coat and protective apron.
    body=[(82,112),(107,91),(158,98),(184,126),(177,223),(150,246),(91,230),(69,197)]
    im.alpha_composite(masked_material((w,h),(77,78,66),body,77,22)); d=ImageDraw.Draw(im); bevel_polygon(d,body,(25,31,29,255),(173,164,131,255),5)
    d.polygon([(98,132),(157,126),(166,211),(145,231),(103,216)],fill=(52,59,53,255),outline=(151,148,119,255),width=4)
    d.polygon([(111,137),(149,133),(156,194),(106,195)],fill=(100,67,46,255),outline=(202,166,98,255),width=3)
    d.line((112,153,153,149),fill=(224,184,101,255),width=5); d.line((111,170,155,168),fill=(35,41,38,255),width=4)
    d.rectangle((91,121,111,145),fill=(159,45,44,255),outline=(220,177,103,255),width=2)
    # Arms, gloves and a large insulated wrench held clear of the silhouette.
    d.polygon([(72,128),(91,117),(105,143),(88,181),(65,188),(55,169)],fill=(77,83,70,255),outline=(171,164,132,255),width=4)
    d.ellipse((50,166,78,193),fill=(36,41,37,255),outline=(200,170,106,255),width=3)
    d.line((63,180,38,222,26,248),fill=(191,183,156,255),width=8,joint="curve"); d.line((25,242,16,250,27,260,39,253),fill=(216,177,97,255),width=5,joint="curve")
    d.polygon([(173,129),(193,119),(209,139),(209,173),(190,188),(173,169)],fill=(88,84,66,255),outline=(187,166,119,255),width=4)
    d.ellipse((191,169,217,194),fill=(35,41,38,255),outline=(191,164,104,255),width=3)
    # Broad welding hood, amber visor and paired filter cans.
    d.rectangle((110,83,153,111),fill=(42,48,43,255))
    d.polygon([(84,31),(106,17),(161,20),(183,43),(177,91),(157,107),(101,99),(78,72)],fill=(89,92,75,255),outline=(207,184,132,255),width=5)
    d.polygon([(91,48),(169,44),(165,67),(94,71)],fill=(28,37,37,255),outline=(197,153,81,255),width=3)
    d.line((100,54,159,51),fill=(239,175,74,255),width=5)
    for x in (96,165):
        d.ellipse((x-9,70,x+9,90),fill=(128,43,41,255),outline=(220,177,103,255),width=3)
    d.line((103,103,155,111),fill=(213,172,93,255),width=4)
    return overlay_grain(im,16,781)

def make_light_vehicle():
    """Compact six-wheel reconnaissance carrier, shown in fixed 3/4 view."""
    w,h=420,300; im=Image.new("RGBA",(w,h),(0,0,0,0))
    sh=Image.new("RGBA",(w,h),(0,0,0,0)); ImageDraw.Draw(sh).ellipse((25,205,398,281),fill=(0,0,0,175)); sh=sh.filter(ImageFilter.GaussianBlur(13)); im=Image.alpha_composite(im,sh); d=ImageDraw.Draw(im)
    # Six exposed tires with pale hubs and a thick field chassis.
    for x in (66,173,280):
        for y in (170,230):
            d.ellipse((x-31,y-31,x+31,y+31),fill=(19,24,24,255),outline=(143,139,116,255),width=5)
            d.ellipse((x-19,y-19,x+19,y+19),fill=(62,68,60,255),outline=(181,166,126,255),width=3)
            d.ellipse((x-7,y-7,x+7,y+7),fill=(185,159,104,255))
            for a in range(0,360,60):
                q=math.radians(a); d.line((x+math.cos(q)*21,y+math.sin(q)*21,x+math.cos(q)*28,y+math.sin(q)*28),fill=(183,171,137,255),width=3)
    hull=[(28,151),(89,105),(275,96),(383,149),(375,218),(321,243),(72,231),(30,199)]
    im.alpha_composite(masked_material((w,h),(82,88,74),hull,109,22)); d=ImageDraw.Draw(im); bevel_polygon(d,hull,(19,25,24,255),(195,180,137,255),5)
    d.polygon([(34,151),(92,112),(272,103),(356,148),(315,171),(75,178)],fill=(131,128,103,255),outline=(207,189,145,255),width=4)
    d.polygon([(75,178),(315,171),(371,193),(320,224),(74,215),(34,190)],fill=(52,61,56,255),outline=(122,128,110,255),width=4)
    # Armored cabin, open sensor cupola and short remotely operated cannon.
    d.polygon([(107,137),(139,88),(239,82),(286,125),(278,163),(131,168)],fill=(92,99,84,255),outline=(208,192,150,255),width=5)
    d.polygon([(124,132),(148,99),(232,94),(264,126),(249,141),(130,147)],fill=(38,48,48,255),outline=(164,160,132,255),width=3)
    d.line((152,100,158,137),fill=(107,125,122,255),width=4); d.line((237,98,247,136),fill=(107,125,122,255),width=4)
    d.polygon([(178,87),(199,63),(235,67),(253,92),(240,108),(182,105)],fill=(122,116,92,255),outline=(212,185,130,255),width=4)
    d.line((222,78,311,45),fill=(43,49,46,255),width=17); d.line((232,76,311,46),fill=(197,183,144,255),width=4)
    d.polygon([(300,37),(333,31),(340,43),(313,56)],fill=(32,39,38,255),outline=(195,178,135,255),width=3)
    d.rectangle((85,160,126,171),fill=(166,43,45,255)); d.rectangle((93,163,107,168),fill=(236,190,100,255))
    # Headlamps, tow hooks, antenna and service hatches.
    for x in (45,354):
        d.ellipse((x-8,151,x+8,166),fill=(228,184,98,255),outline=(29,34,32,255),width=3)
    for x in (137,277):
        d.rounded_rectangle((x,144,x+34,157),radius=3,fill=(71,78,67,255),outline=(187,172,132,255),width=2)
        d.ellipse((x+6,147,x+11,152),fill=(223,187,117,255)); d.ellipse((x+24,147,x+29,152),fill=(223,187,117,255))
    d.line((291,94,284,31),fill=(158,160,141,255),width=3); d.ellipse((280,24,288,32),fill=(199,47,44,255))
    return overlay_grain(im,14,782)

def make_building(kind):
    """Small tactical building set sharing lighting and Kor industrial materials."""
    w,h=420,330; im=Image.new("RGBA",(w,h),(0,0,0,0))
    sh=Image.new("RGBA",(w,h),(0,0,0,0)); ImageDraw.Draw(sh).ellipse((18,263,404,316),fill=(0,0,0,170)); sh=sh.filter(ImageFilter.GaussianBlur(15)); im=Image.alpha_composite(im,sh); d=ImageDraw.Draw(im)
    if kind=="barracks":
        footprint=[(35,166),(99,112),(299,105),(383,160),(365,274),(106,284),(37,238)]
        roof=(111,119,96); side=(48,57,53)
    elif kind=="generator":
        footprint=[(47,171),(105,121),(295,111),(370,159),(356,272),(98,280),(42,232)]
        roof=(104,108,88); side=(43,52,50)
    else:
        footprint=[(36,175),(97,118),(305,113),(383,164),(361,272),(99,284),(33,237)]
        roof=(105,108,91); side=(47,55,51)
    d.polygon(footprint,fill=side,outline=(196,185,147,255),width=5)
    d.polygon([(footprint[0][0]+7,footprint[0][1]),(footprint[1][0]+5,footprint[1][1]+4),(footprint[2][0]-4,footprint[2][1]+5),(footprint[3][0]-7,footprint[3][1]),(footprint[4][0]-5,footprint[4][1]-8),(footprint[5][0]-5,footprint[5][1]-15),(footprint[6][0]+4,footprint[6][1]-7)],fill=roof,outline=(207,194,151,255),width=4)
    d.polygon([(51,178),(112,170),(106,270),(43,232)],fill=(37,44,42,255),outline=(119,128,111,255),width=3)
    # Service facade with individual category identifiers.
    d.rectangle((139,179,283,268),fill=(28,35,35,255),outline=(157,158,132,255),width=4)
    d.rectangle((153,191,270,203),fill=(143,41,44,255)); d.rectangle((161,195,183,200),fill=(226,182,96,255))
    if kind=="barracks":
        d.rectangle((176,211,245,267),fill=(44,52,49,255),outline=(182,172,137,255),width=3)
        d.rectangle((187,220,234,265),fill=(22,29,29,255)); d.line((211,222,211,264),fill=(133,135,117,255),width=3)
        for x in (111,292):
            d.rectangle((x,189,x+31,224),fill=(71,75,65,255),outline=(187,169,128,255),width=3)
            d.line((x+7,196,x+24,216),fill=(205,174,105,255),width=3)
        label=("ARMORY",(223,182,95,255))
    elif kind=="generator":
        # Twin exhaust stacks and transformer banks.
        for x in (115,293):
            d.rectangle((x,93,x+29,171),fill=(42,50,48,255),outline=(179,170,137,255),width=4)
            d.ellipse((x-2,85,x+31,101),fill=(104,108,91,255),outline=(194,183,145,255),width=3)
            for y in range(110,161,13): d.line((x+4,y,x+25,y),fill=(168,159,128,255),width=3)
        for x in range(149,273,20):
            d.rectangle((x,217,x+13,254),fill=(78,88,77,255),outline=(176,166,130,255),width=2)
            d.line((x+6,221,x+6,250),fill=(210,168,83,255),width=2)
        label=("POWER",(226,178,85,255))
    else:
        # Loading canopy, roller bay and visible ore processing drum.
        d.polygon([(127,159),(178,132),(308,135),(343,161),(320,177),(151,178)],fill=(91,91,73,255),outline=(202,183,139,255),width=4)
        d.rectangle((168,188,235,268),fill=(23,29,29,255),outline=(154,156,131,255),width=4)
        d.ellipse((258,204,320,263),fill=(61,69,62,255),outline=(198,178,131,255),width=4)
        d.ellipse((273,218,305,250),fill=(30,38,37,255),outline=(138,149,128,255),width=3)
        d.line((286,223,290,245),fill=(206,162,82,255),width=4)
        label=("ORE",(230,177,84,255))
    # Roof vents, access ladder, facade bolts and warm machine indicator.
    for x in (113,316):
        d.rounded_rectangle((x,135,x+43,153),radius=3,fill=(46,53,49,255),outline=(172,165,135,255),width=3)
        for xx in range(x+7,x+39,8): d.line((xx,139,xx,150),fill=(183,172,137,255),width=2)
    d.rectangle((334,182,354,246),fill=(76,79,66,255),outline=(170,161,130,255),width=3)
    for y in range(191,240,9): d.line((338,y,350,y),fill=(205,170,99,255),width=2)
    d.ellipse((300,177,309,186),fill=(236,180,82,255))
    return overlay_grain(im,14,790+len(kind))

def make_hud():
    w,h=2048,256
    im=texture((w,h),(19,24,25),20,170)
    d=ImageDraw.Draw(im)
    d.rectangle((0,0,w-1,h-1),outline=(132,135,123,255),width=5)
    d.line((0,8,w,8),fill=(199,178,129,180),width=3)
    d.line((0,h-9,w,h-9),fill=(5,8,9,240),width=5)
    # Machined inset panels, riveted corners, seams and restrained Kor red enamel.
    for x in range(16,w,256):
        d.line((x,17,x,h-18),fill=(4,7,9,220),width=5)
        d.line((x+4,18,x+4,h-18),fill=(111,115,106,145),width=2)
        d.line((x+12,28,x+12,h-29),fill=(179,72,55,80),width=2)
        for y in (25,h-26):
            d.ellipse((x-6,y-6,x+6,y+6),fill=(177,153,111,255),outline=(15,20,21,255),width=3)
            d.line((x-3,y,x+3,y),fill=(38,42,40,255),width=2)
    # Diagonal warning enamel at both edges.
    for x in range(0,76,24): d.polygon([(x,220),(x+12,220),(x+46,256),(x+34,256)],fill=(134,36,38,170))
    return overlay_grain(im,14,188)

def make_button(red=False):
    w,h=760,160; base=(69,36,39) if red else (36,44,43)
    im=texture((w,h),base,10,130 if red else 131); d=ImageDraw.Draw(im)
    d.rectangle((3,3,w-4,h-4),outline=(171,145,107,255),width=5)
    d.line((11,12,w-12,12),fill=(221,195,148,210),width=3)
    d.line((13,18,13,h-19),fill=(213,179,118,215),width=4)
    d.line((19,h-13,w-17,h-13),fill=(6,10,12,245),width=5)
    d.line((w-13,18,w-13,h-19),fill=(10,14,15,220),width=5)
    # Enamel command stripe and inset illumination.
    stripe=(181,48,46,255) if red else (201,158,80,255)
    d.polygon([(27,26),(39,26),(53,42),(53,h-43),(39,h-27),(27,h-27)],fill=stripe)
    d.line((57,24,w-25,24),fill=(255,225,166,42),width=2)
    for x in (19,w-20):
        for y in (19,h-20):
            d.ellipse((x-7,y-7,x+7,y+7),fill=(176,163,133,255),outline=(13,18,19,255),width=3)
            d.line((x-3,y-3,x+3,y+3),fill=(43,49,47,255),width=2)
    return overlay_grain(im,10,321 if red else 322)

def make_region(kind, base):
    w=h=320; cx,cy=160,160; points=[(cx,5),(311,82),(311,238),(cx,315),(9,238),(9,82)]
    im=texture((w,h),base,24,500+len(kind)).convert("RGBA"); mask=Image.new("L",(w,h),0); ImageDraw.Draw(mask).polygon(points,fill=255); im.putalpha(mask)
    d=ImageDraw.Draw(im); rng=random.Random(710+len(kind))
    d.line(points+[points[0]],fill=(191,180,138,210),width=7,joint="curve")
    d.line((cx,10,305,82),fill=(220,202,157,50),width=3)
    if kind=="Granary":
        for y in range(105,266,22):
            d.line((40,y,276,y-24),fill=(176,157,105,115),width=5)
            d.line((42,y+5,278,y-19),fill=(60,73,48,170),width=8)
        for x,y in [(74,143),(216,111),(160,214),(240,200)]:
            d.ellipse((x-10,y-9,x+10,y+9),fill=(93,122,68,255));d.line((x,y+4,x+2,y+25),fill=(62,83,48,255),width=4)
    elif kind=="Mine":
        for _ in range(13):
            x=rng.randrange(42,278); y=rng.randrange(90,260); r=rng.randrange(8,22)
            d.polygon([(x-r,y+r//2),(x-r//2,y-r),(x+r//2,y-r//2),(x+r,y+r),(x-r//3,y+r)],fill=(93,99,91,210),outline=(190,176,135,160))
        d.line((54,223,248,111),fill=(37,45,44,255),width=11);d.line((58,219,252,107),fill=(172,142,93,160),width=3)
    elif kind=="Solar field":
        for row in range(4):
            y=112+row*36
            for col in range(3):
                x=58+col*70+row*8
                d.polygon([(x,y),(x+51,y-9),(x+59,y+16),(x+9,y+25)],fill=(39,65,73,255),outline=(201,186,139,220),width=3)
                for k in range(1,4): d.line((x+k*13,y-2-k*2,x+9+k*13,y+21-k*2),fill=(129,166,171,180),width=2)
                d.line((x+9,y+24,x+10,y+32),fill=(67,72,65,255),width=3)
    else:
        # Archive: fortified data vaults and antenna masts.
        for x,y,wid,hei in [(73,124,58,87),(153,98,77,124),(234,147,42,64)]:
            d.polygon([(x,y+10),(x+12,y),(x+wid,y+5),(x+wid-4,y+hei),(x+5,y+hei-3)],fill=(70,82,84,245),outline=(190,190,160,220),width=4)
            for yy in range(y+22,y+hei-6,18): d.rectangle((x+13,yy,x+wid-10,yy+4),fill=(196,146,74,215))
        d.line((197,99,205,51),fill=(205,183,132,255),width=3);d.ellipse((200,45,210,55),fill=(193,51,47,255))
    # Ground strata and scattered vegetation are consistent across biome types.
    for _ in range(100):
        x=rng.randrange(24,296); y=rng.randrange(30,290)
        if mask.getpixel((x,y)):
            col=(222,194,140,rng.randrange(20,95)) if rng.random()<.56 else (9,15,15,rng.randrange(30,90))
            d.ellipse((x,y,x+rng.randrange(1,5),y+rng.randrange(1,3)),fill=col)
    return im

def make_strategic_map():
    w,h=1200,1000; im=texture((w,h),(32,42,39),26,839).convert("RGBA"); d=ImageDraw.Draw(im)
    # Low contrast landforms, fault lines, rivers and roads under the region pieces.
    rng=random.Random(918)
    relief=Image.new("RGBA",(w,h),(0,0,0,0)); rd=ImageDraw.Draw(relief)
    for i in range(28):
        x=rng.randrange(-120,w+120); y=rng.randrange(-60,h+60); rx=rng.randrange(65,210); ry=rng.randrange(30,115)
        rd.ellipse((x-rx,y-ry,x+rx,y+ry),fill=(107,111,80,rng.randrange(5,13)))
    relief=relief.filter(ImageFilter.GaussianBlur(34)); im=Image.alpha_composite(im,relief); d=ImageDraw.Draw(im)
    for i in range(16):
        x=rng.randrange(-80,w+80); y=rng.randrange(-40,h+40); rx=rng.randrange(55,180); ry=rng.randrange(24,90)
        d.ellipse((x-rx,y-ry,x+rx,y+ry),outline=(164,160,119,rng.randrange(5,13)),width=1)
    for j in range(5):
        pts=[]; x=rng.randrange(w); y=0
        while y<h:
            pts.append((x,y)); x+=rng.randrange(-55,56); y+=rng.randrange(35,90)
        d.line(pts,fill=(104,137,129,19),width=rng.randrange(3,7),joint="curve")
        d.line([(px+3,py) for px,py in pts],fill=(201,181,129,11),width=2)
    # Scattered cartographic elevation marks and settlement glyphs.
    for _ in range(400):
        x=rng.randrange(w); y=rng.randrange(h); r=rng.randrange(2,8)
        d.line((x-r,y,x+r,y),fill=(177,167,126,rng.randrange(12,35)),width=1)
    return overlay_grain(im,13,987)

def main():
    make_menu()
    for i in range(6): make_tile(i).save(OUT/f"ground-{i}.png")
    make_tile(7,True).save(OUT/"ground-blight.png")
    make_worker().save(OUT/"kor-pioneer.png")
    make_guardian().save(OUT/"kor-guardian.png")
    make_tank().save(OUT/"kor-sentinel-tank.png")
    make_base().save(OUT/"kor-command-habitat.png")
    make_purifier().save(OUT/"kor-purifier.png")
    make_blight().save(OUT/"blight-stalker.png")
    make_ore().save(OUT/"alloy-outcrop.png")
    make_rocks().save(OUT/"terrain-rocks.png")
    make_supply_cache().save(OUT/"supply-cache.png")
    make_engineer().save(OUT/"kor-engineer.png")
    make_light_vehicle().save(OUT/"kor-light-carrier.png")
    for kind, filename in [("barracks","kor-barracks"),("generator","kor-generator"),("extractor","kor-extractor")]:
        make_building(kind).save(OUT/(filename+".png"))
    make_hud().save(OUT/"kor-hud-metal.png")
    make_button(True).save(OUT/"kor-button-red.png")
    make_button(False).save(OUT/"kor-button-steel.png")
    for name,base in [("Granary",(48,69,48)),("Mine",(57,62,57)),("Solar field",(73,65,48)),("Archive",(42,61,69))]:
        make_region(name,base).save(OUT/("region-"+name.lower().replace(" ","-")+".png"))
    make_strategic_map().save(OUT/"strategic-map.png")
    print(f"Wrote original high-resolution art to {OUT}")

if __name__ == "__main__": main()
