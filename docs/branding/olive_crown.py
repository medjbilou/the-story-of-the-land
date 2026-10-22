"""Draws the olive tree into app-icon.svg (between its "the olive tree" comment and the gold frame), seeded, so the crown is
the same every run. Then: app-icon-fullbleed.svg is the same file with viewBox="100 100 824 824" (the web favicon), and
rsvg-convert -w 1024 app-icon.svg -o ../../unity/Assets/Branding/AppIcon.png makes the player icon."""
import math, random
random.seed(7)
s=open('app-icon.svg').read()
i=s.index('    <!-- the olive tree'); j=s.index('  </g>\n\n  <!-- gold frame')
cx, cy = 512, 452
def outline(t):  # dome: round top, lumpy flatter underside
    x = cx + 246*math.cos(t)
    y = cy - 158*math.sin(t) if math.sin(t) > 0 else cy - 74*math.sin(t) + 10*math.sin(5*t)
    return x, y
def leaf(x, y, ang, L, W, fill):
    # lanceolate leaf as a quadratic lens
    dx, dy = math.cos(ang), math.sin(ang); nx, ny = -dy, dx
    tx, ty = x+dx*L, y+dy*L; mx, my = x+dx*L/2, y+dy*L/2
    return (f'<path d="M{x:.1f} {y:.1f} Q{mx+nx*W:.1f} {my+ny*W:.1f} {tx:.1f} {ty:.1f} '
            f'Q{mx-nx*W:.1f} {my-ny*W:.1f} {x:.1f} {y:.1f} Z" fill="{fill}"/>')
dark, mid, silver = '#1e2a1b', '#2c3a26', '#a7b28a'
out=[]
# canopy body: lumpy blob from many overlapping ellipses inside the dome
blob=[]
pts=' '.join(f'{cx+(outline(2*math.pi*k/90)[0]-cx)*0.9:.1f},{cy+(outline(2*math.pi*k/90)[1]-cy)*0.9:.1f}' for k in range(90))
blob.append(f'<polygon points="{pts}"/>')
for k in range(60):
    t = random.uniform(0, 2*math.pi); r = random.uniform(0.55, 0.9)
    x0, y0 = outline(t); x = cx+(x0-cx)*r; y = cy+(y0-cy)*r
    blob.append(f'<ellipse cx="{x:.0f}" cy="{y:.0f}" rx="{random.uniform(46,74):.0f}" ry="{random.uniform(30,46):.0f}"/>')
out.append(f'<g fill="{dark}">'+''.join(blob)+'</g>')
# feathery edge: leaves pointing outwards around the dome
edge=[]
for k in range(150):
    t = 2*math.pi*k/150 + random.uniform(-0.02,0.02)
    x, y = outline(t); x += random.uniform(-14,6)*math.cos(t); y -= random.uniform(-10,6)*math.sin(t)
    ang = math.atan2(y-cy, x-cx) + random.uniform(-0.5,0.5)
    edge.append(leaf(x-math.cos(ang)*20, y-math.sin(ang)*20, ang, random.uniform(34,48), random.uniform(7,10), dark))
out.append(''.join(edge))
# silver undersides catching the light, mostly on the upper canopy
sil=[]
for k in range(44):
    t = random.uniform(0.35, math.pi-0.2); r = random.uniform(0.35, 0.9)
    x0, y0 = outline(t); x = cx+(x0-cx)*r; y = cy+(y0-cy)*r
    ang = random.uniform(-math.pi, math.pi)
    sil.append(leaf(x, y, ang, random.uniform(22,30), random.uniform(4.5,6.5), silver))
out.append(f'<g opacity="0.85">'+''.join(sil)+'</g>')
trunk='''<g fill="#17130f">
      <path d="M418 786 C446 774 462 752 466 722 C470 690 452 664 456 632 C460 598 478 574 470 540 L490 532
               C500 560 500 586 506 600 C512 580 512 552 508 516 L528 514 C532 548 532 580 528 604
               C540 582 556 560 570 532 L590 540 C578 572 566 602 566 634 C566 666 552 694 556 724
               C560 754 578 774 606 786 Z"/>
      <path d="M470 560 C440 540 410 530 380 526 L384 508 C418 512 450 524 480 546 Z"/>
      <path d="M572 556 C604 534 634 524 664 520 L664 538 C636 542 608 552 580 572 Z"/>
    </g>
    <path d="M502 698 C496 676 502 658 512 646 C522 658 528 676 522 698 C518 712 506 712 502 698 Z" fill="url(#sun)"/>'''
tree='    <!-- the olive tree: a short, hollow, gnarled trunk under a broad silver-green crown, dark against the sun -->\n    '+trunk+'\n    '+'\n    '.join(out)+'\n'
s=s[:i]+tree+s[j:]
open('app-icon.svg','w').write(s)
