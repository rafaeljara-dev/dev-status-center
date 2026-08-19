# Genera los artboards de la propuesta. Los paths de logo se inyectan desde logos.json
# (simple-icons) para no transcribir a mano cadenas de 1800 caracteres.
import json
import pathlib

L = json.loads(pathlib.Path("logos.json").read_text(encoding="utf-8"))
HERE = pathlib.Path(".")

HEAD = """<!doctype html>
<html>
<head>
  <meta charset="utf-8">
  <script src="./support.js"></script>
</head>
<body>
<x-dc>
<helmet>
  <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin="anonymous">
  <link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Space+Grotesk:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500;600&display=swap">
  <style>
    body { margin: 0; background: #05070B; font-family: "Space Grotesk", "Segoe UI Variable Text", "Segoe UI", system-ui, sans-serif; -webkit-font-smoothing: antialiased; }
    a { color: #62D99C; } a:hover { color: #8AE7B7; }
%EXTRA%
  </style>
</helmet>
"""

FOOT = """</x-dc>
<script data-dc-script data-props='%PROPS%'>
class Component extends DCLogic {
  renderVals() {
    return { accent: this.props.accent ?? '#62D99C' };
  }
}
</script>
</body>
</html>
"""

ACCENT_PROP = ('{"accent":{"editor":"color","default":"#62D99C",'
               '"options":["#62D99C","#7CE0FF","#A98AF4","#F6C85F"],"section":"Estilo"},'
               '"$preview":{"width":%d,"height":%d}}')


def logo(name, size=15, color="currentColor"):
    return (f'<svg width="{size}" height="{size}" viewBox="0 0 24 24" fill="{color}" '
            f'aria-hidden="true"><path d="{L[name]}"></path></svg>')


def face(size=20, accent="{{accent}}", state="calm", blink=True):
    """Carita: circulo con ojos. El estado cambia boca y ojos, no el color de fondo."""
    eye_cls = ' class="ojo"' if blink else ""
    if state == "sleep":
        eyes = (f'<path d="M6.6 10.8h4M13.4 10.8h4" stroke="{accent}" stroke-width="1.6" '
                f'stroke-linecap="round"></path>')
        mouth = f'<path d="M9.6 15.6h4.8" stroke="{accent}" stroke-width="1.5" stroke-linecap="round"></path>'
    elif state == "visor":
        eyes = f'<rect x="5.4" y="8.8" width="13.2" height="4.4" rx="2.2" fill="{accent}"></rect>'
        mouth = f'<path d="M9.4 16.2h5.2" stroke="{accent}" stroke-width="1.5" stroke-linecap="round"></path>'
    else:
        r = "2.1" if state == "alarm" else "1.5"
        eyes = (f'<rect{eye_cls} x="7.1" y="9.1" width="3" height="3" rx="1.5" fill="{accent}"></rect>'
                f'<rect{eye_cls} x="13.9" y="9.1" width="3" height="3" rx="1.5" fill="{accent}"></rect>')
        if state == "alarm":
            eyes = (f'<circle{eye_cls} cx="8.6" cy="10.6" r="2.1" fill="{accent}"></circle>'
                    f'<circle{eye_cls} cx="15.4" cy="10.6" r="2.1" fill="{accent}"></circle>')
        mouths = {
            "calm":  'M8.4 15.1c1 1 2.2 1.5 3.6 1.5s2.6-.5 3.6-1.5',
            "flat":  'M8.7 15.9h6.6',
            "worry": 'M8.4 16.6c1-1 2.2-1.5 3.6-1.5s2.6.5 3.6 1.5',
            "alarm": 'M8.4 17c1-1.2 2.2-1.8 3.6-1.8s2.6.6 3.6 1.8',
        }
        mouth = (f'<path d="{mouths[state]}" stroke="{accent}" stroke-width="1.5" fill="none" '
                 f'stroke-linecap="round"></path>')
    return (f'<svg width="{size}" height="{size}" viewBox="0 0 24 24" fill="none" aria-hidden="true">'
            f'<circle cx="12" cy="12" r="10" stroke="{accent}" stroke-width="1.5"></circle>'
            f'{eyes}{mouth}</svg>')


MONO = "'JetBrains Mono', ui-monospace, monospace"

# ---------------------------------------------------------------- Hibrido
BLINK_CSS = """    /* La carita parpadea. Solo mientras el popup esta abierto: al ocultarse, WPF
       detiene el Storyboard y el costo vuelve a cero (NFR-004). */
    @keyframes parpadeo { 0%, 91%, 100% { transform: scaleY(1); } 95% { transform: scaleY(0.12); } }
    .ojo { transform-box: fill-box; transform-origin: center; animation: parpadeo 5.5s ease-in-out infinite; }
    /* Reflejo especular que recorre el borde superior del cristal, muy lento y muy tenue. */
    @keyframes brillo { 0% { transform: translateX(-60%); } 100% { transform: translateX(160%); } }
    .brillo { animation: brillo 9s linear infinite; }
    @media (prefers-reduced-motion: reduce) { .ojo, .brillo { animation: none; } }"""

svc_rows = [
    ("openai", "OpenAI", "17.43", 100, 1.0),
    ("claude", "Anthropic", "11.58", 66, 0.65),
    ("vercel", "Vercel", "7.24", 42, 0.42),
    ("neon", "Neon", "3.26", 19, 0.3),
]
rows_html = ""
for i, (key, name, amount, pct, op) in enumerate(svc_rows):
    last = "" if i < len(svc_rows) - 1 else "border-bottom: none;"
    rows_html += f"""
        <div style="padding: 11px 0; border-bottom: 1px solid rgba(255,255,255,0.05); {last}">
          <div style="display: flex; align-items: center; gap: 10px;">
            <span style="color: #8E9CB0; display: flex;">{logo(key)}</span>
            <span style="flex-grow: 1; font-size: 13px; font-weight: 500;">{name}</span>
            <span style="font-family: {MONO}; font-size: 13px; font-variant-numeric: tabular-nums;">{amount}</span>
          </div>
          <div style="height: 3px; background: rgba(255,255,255,0.07); border-radius: 999px; margin-top: 8px;">
            <div style="width: {pct}%; height: 3px; background: {{{{accent}}}}; opacity: {op}; border-radius: 999px;"></div>
          </div>
        </div>"""

blocks = ""
for i in range(15):
    if i < 8:
        blocks += '<div style="flex-grow: 1; height: 9px; background: {{accent}}; border-radius: 1px;"></div>'
    elif i < 10:
        blocks += '<div style="flex-grow: 1; height: 9px; background: {{accent}}; opacity: 0.42; border-radius: 1px;"></div>'
    else:
        blocks += '<div style="flex-grow: 1; height: 9px; background: rgba(255,255,255,0.09); border-radius: 1px;"></div>'

tabs = [
    (face(19), "", True),
    (None, "IA", False),
    (None, "CLOUD", False),
    (None, "PAGOS", False),
]
tabs_html = ""
for svg, label, active in tabs:
    color = "#F5F7FA" if active else "#7C8CA1"
    weight = "600" if active else "500"
    under = "{{accent}}" if active else "transparent"
    inner = svg if svg else f'<span style="font-size: 11px; font-weight: {weight}; letter-spacing: 0.07em; color: {color};">{label}</span>'
    tabs_html += (f'<div style="display: flex; align-items: center; justify-content: center; '
                  f'padding: 9px 0 10px 0; border-bottom: 2px solid {under}; margin-bottom: -1px;">{inner}</div>')

hibrido = HEAD.replace("%EXTRA%", BLINK_CSS) + f"""<div style="width: 620px; height: 880px; box-sizing: border-box; position: relative; overflow: hidden; border-radius: 16px; background: linear-gradient(150deg, #131A2A 0%, #0C1018 55%, #161226 100%);">

  <div style="position: absolute; left: -90px; top: 90px; width: 340px; height: 340px; border-radius: 50%; background: #2B6EA8; opacity: 0.32; filter: blur(70px);"></div>
  <div style="position: absolute; right: -70px; top: 430px; width: 300px; height: 300px; border-radius: 50%; background: #7A4BA8; opacity: 0.3; filter: blur(70px);"></div>
  <div style="position: absolute; left: 210px; bottom: -80px; width: 320px; height: 260px; border-radius: 50%; background: #1E8F72; opacity: 0.22; filter: blur(70px);"></div>
  <div style="position: absolute; left: 40px; top: 26px; font-size: 10px; letter-spacing: 0.16em; color: rgba(255,255,255,0.34);">TU ESCRITORIO (SIMULADO, PARA QUE SE VEA EL CRISTAL)</div>

  <div style="position: absolute; left: 105px; top: 68px; width: 410px; height: 762px; box-sizing: border-box; border-radius: 14px; overflow: hidden; background: rgba(13, 17, 24, 0.66); backdrop-filter: blur(26px) saturate(150%); -webkit-backdrop-filter: blur(26px) saturate(150%); border: 1px solid rgba(255,255,255,0.09); box-shadow: 0 26px 70px rgba(0,0,0,0.55), inset 0 1px 0 rgba(255,255,255,0.10); color: #F5F7FA; display: flex; flex-direction: column;">

    <div style="position: absolute; left: 0; top: 0; right: 0; height: 1px; overflow: hidden; pointer-events: none;">
      <div class="brillo" style="width: 40%; height: 1px; background: linear-gradient(90deg, transparent, rgba(255,255,255,0.55), transparent);"></div>
    </div>

    <div style="display: flex; align-items: center; gap: 8px; padding: 15px 18px 13px 18px; font-family: {MONO}; font-size: 10px; letter-spacing: 0.1em; color: #75849A;">
      <span style="width: 5px; height: 5px; border-radius: 50%; background: {{{{accent}}}};"></span>
      <span style="color: #B3C0D0;">NORMAL</span><span>·</span><span>SYNC 8M</span><span>·</span><span>5 SVC</span>
      <span style="flex-grow: 1;"></span>
      <span style="color: #F6C85F;">1 AVISO</span>
    </div>

    <div style="padding: 8px 18px 0 18px;">
      <div style="font-size: 9px; letter-spacing: 0.18em; color: #6B7A90;">MES EN CURSO · USD</div>
      <div style="display: flex; align-items: baseline; justify-content: space-between; margin-top: 11px;">
        <div style="font-family: {MONO}; font-size: 44px; font-weight: 500; letter-spacing: -0.03em; font-variant-numeric: tabular-nums; line-height: 1;">128.01</div>
        <div style="text-align: right;">
          <div style="font-size: 9px; letter-spacing: 0.14em; color: #6B7A90;">PROYECCIÓN</div>
          <div style="font-family: {MONO}; font-size: 16px; font-variant-numeric: tabular-nums; color: {{{{accent}}}}; margin-top: 4px;">164.22</div>
        </div>
      </div>
      <div style="display: flex; gap: 3px; margin-top: 18px;">{blocks}</div>
      <div style="display: flex; justify-content: space-between; margin-top: 8px; font-family: {MONO}; font-size: 10px; color: #6B7A90;">
        <span>gastado · proyectado · libre</span><span>200.00</span>
      </div>
    </div>

    <div style="display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); border-bottom: 1px solid rgba(255,255,255,0.07); padding: 0 18px; margin-top: 20px;">{tabs_html}</div>

    <div style="flex-grow: 1; padding: 18px;">
      <div style="display: flex; align-items: baseline; justify-content: space-between; margin-bottom: 4px;">
        <span style="font-size: 10px; font-weight: 600; letter-spacing: 0.14em; color: #8E9CB0;">MAYOR GASTO</span>
        <span style="font-size: 10px; color: #6B7A90;">agosto</span>
      </div>
      {rows_html}

      <div style="margin-top: 18px; border-radius: 10px; padding: 13px 15px; background: rgba(255,255,255,0.055); border: 1px solid rgba(255,255,255,0.075); display: flex; align-items: center; gap: 12px;">
        <span style="color: #8E9CB0; display: flex;">{logo('claude', 17)}</span>
        <div style="flex-grow: 1;">
          <div style="font-size: 10px; font-weight: 500; letter-spacing: 0.12em; color: #8E9CB0;">PRÓXIMO PAGO</div>
          <div style="font-size: 13px; font-weight: 500; margin-top: 4px;">Claude · en 2 días</div>
        </div>
        <div style="font-family: {MONO}; font-size: 17px; font-weight: 500;">20.00</div>
      </div>
    </div>

    <div style="display: flex; align-items: center; justify-content: space-between; padding: 13px 18px 16px 18px; border-top: 1px solid rgba(255,255,255,0.07);">
      <span style="font-family: {MONO}; font-size: 10px; color: #66748A;">SINCRONIZADO HACE 8 MIN</span>
      <div style="display: flex; align-items: center; gap: 16px; color: #8E9CB0;">
        <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M21 12a9 9 0 1 1-2.64-6.36"></path><path d="M21 3v6h-6"></path></svg>
        <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"><path d="M4 6h16"></path><path d="M4 12h16"></path><path d="M4 18h16"></path><circle cx="9" cy="6" r="2" fill="#0D1118"></circle><circle cx="15" cy="12" r="2" fill="#0D1118"></circle><circle cx="8" cy="18" r="2" fill="#0D1118"></circle></svg>
      </div>
    </div>

  </div>
</div>
""" + FOOT.replace("%PROPS%", ACCENT_PROP % (620, 880))
(HERE / "Hibrido.dc.html").write_text(hibrido, encoding="utf-8", newline="\n")

# ---------------------------------------------------------------- Carita
states = [
    ("calm",  "#62D99C", "Normal",        "menos del 70 % del presupuesto"),
    ("flat",  "#F6C85F", "Atención",      "70 % · boca recta, sin drama"),
    ("worry", "#F49A5A", "Importante",    "85 % · ceja caída"),
    ("alarm", "#F06464", "Crítico",       "95 % · ojos grandes"),
    ("sleep", "#778292", "Pausado",       "ojos cerrados, no hay red"),
    ("visor", "#A98AF4", "Modo juego",    "visor: no molesta a nadie"),
]
cards = ""
for st, color, title, sub in states:
    cards += f"""
    <div style="text-align: center;">
      <div style="width: 74px; height: 74px; margin: 0 auto; border-radius: 18px; background: #141A24; border: 1px solid #202836; display: flex; align-items: center; justify-content: center;">{face(38, color, st, blink=(st not in ('sleep','visor')))}</div>
      <div style="font-size: 12px; font-weight: 500; margin-top: 11px;">{title}</div>
      <div style="font-size: 10px; color: #6F7D91; margin-top: 4px; line-height: 1.4;">{sub}</div>
    </div>"""

carita = HEAD.replace("%EXTRA%", BLINK_CSS) + f"""<div style="width: 660px; height: 430px; box-sizing: border-box; background: #0E1219; border: 1px solid #1E2531; border-radius: 14px; padding: 26px 30px; color: #F5F7FA;">
  <div style="font-size: 10px; font-weight: 600; letter-spacing: 0.16em; color: #6F7D91; padding-bottom: 12px; border-bottom: 1px solid #1E2531;">LA CARITA ES EL ESTADO</div>
  <div style="font-size: 11px; color: #99A4B5; margin-top: 14px; line-height: 1.55; max-width: 560px;">Hoy el encabezado dibuja el punto de estado dos veces y no dice nada que el color no dijera ya. La carita ocupa ese hueco: es la primera pestaña, es el icono del tray, y su gesto comunica el estado antes de que leas una cifra. El color sigue siendo tu paleta de umbrales, sin inventar ninguno.</div>

  <div style="display: grid; grid-template-columns: repeat(6, minmax(0, 1fr)); gap: 16px; margin-top: 24px;">{cards}</div>

  <div style="display: flex; gap: 30px; margin-top: 28px; padding-top: 18px; border-top: 1px solid #1E2531;">
    <div style="flex-grow: 1;">
      <div style="font-size: 10px; font-weight: 600; letter-spacing: 0.14em; color: #6F7D91;">PARPADEO</div>
      <div style="font-size: 11px; color: #99A4B5; margin-top: 8px; line-height: 1.5;">Un parpadeo cada 5,5 s, de 180 ms. Solo mientras el popup está visible: al ocultarse se detiene el Storyboard y el costo vuelve a cero. Respeta «reducir movimiento» del sistema.</div>
    </div>
    <div style="flex-grow: 1;">
      <div style="font-size: 10px; font-weight: 600; letter-spacing: 0.14em; color: #6F7D91;">A DECIDIR</div>
      <div style="font-size: 11px; color: #99A4B5; margin-top: 8px; line-height: 1.5;">NFR-006 dice «sin animaciones permanentes». Un parpadeo en bucle mientras miras el popup es defendible; si prefieres ser estricto, que parpadee dos veces al abrir y se quede quieto.</div>
    </div>
  </div>
</div>
""" + FOOT.replace("%PROPS%", '{"$preview":{"width":660,"height":430}}').replace(
    "return { accent: this.props.accent ?? '#62D99C' };", "return {};")
(HERE / "Carita.dc.html").write_text(carita, encoding="utf-8", newline="\n")

# ---------------------------------------------------------------- Marcas
brands = [("openai", "OpenAI", "#412991"), ("claude", "Anthropic", "#D97757"),
          ("vercel", "Vercel", "#FFFFFF"), ("cloudflare", "Cloudflare", "#F38020"),
          ("neon", "Neon", "#00E599")]
mono_row = "".join(
    f'<div style="text-align: center;"><span style="color: #8E9CB0; display: inline-flex;">{logo(k, 22)}</span>'
    f'<div style="font-size: 10px; color: #6F7D91; margin-top: 9px;">{n}</div></div>' for k, n, _ in brands)
color_row = "".join(
    f'<div style="text-align: center;"><span style="color: {c}; display: inline-flex;">{logo(k, 22, "currentColor")}</span>'
    f'<div style="font-family: {MONO}; font-size: 9px; color: #6F7D91; margin-top: 9px;">{c}</div></div>'
    for k, n, c in brands)

marcas = HEAD.replace("%EXTRA%", "") + f"""<div style="width: 660px; height: 430px; box-sizing: border-box; background: #0E1219; border: 1px solid #1E2531; border-radius: 14px; padding: 26px 30px; color: #F5F7FA;">
  <div style="font-size: 10px; font-weight: 600; letter-spacing: 0.16em; color: #6F7D91; padding-bottom: 12px; border-bottom: 1px solid #1E2531;">MARCAS</div>

  <div style="display: flex; align-items: baseline; gap: 12px; margin-top: 20px;">
    <span style="font-size: 12px; font-weight: 600;">Monocromo</span>
    <span style="font-size: 10px; color: {{{{accent}}}}; border: 1px solid {{{{accent}}}}; border-radius: 999px; padding: 2px 8px;">RECOMENDADO</span>
  </div>
  <div style="display: grid; grid-template-columns: repeat(5, minmax(0, 1fr)); gap: 14px; margin-top: 16px;">{mono_row}</div>
  <div style="font-size: 11px; color: #99A4B5; margin-top: 14px; line-height: 1.5; max-width: 560px;">La silueta ya identifica al servicio; el color no aporta nada y sí compite. Cinco marcas a todo color meten cinco acentos nuevos en una interfaz que se sostiene sobre uno solo, y peor: chocan con tus colores de umbral, que sí significan algo.</div>

  <div style="font-size: 12px; font-weight: 600; margin-top: 24px; padding-top: 18px; border-top: 1px solid #1E2531;">A color</div>
  <div style="display: grid; grid-template-columns: repeat(5, minmax(0, 1fr)); gap: 14px; margin-top: 16px;">{color_row}</div>
  <div style="font-size: 11px; color: #99A4B5; margin-top: 14px; line-height: 1.5; max-width: 560px;">Reservar el color de marca para un solo momento: cuando ese servicio es el que disparó una alerta. Así el color vuelve a ser señal.</div>

  <div style="font-size: 10px; color: #6F7D91; margin-top: 20px; line-height: 1.5;">Trazados de simple-icons (colección CC0). Los logos son marcas registradas de sus dueños; aquí se usan para identificar el servicio que monitoreas. Al empotrarlos en el .exe: SVG como recurso, 16 px, un solo trazado por marca.</div>
</div>
""" + FOOT.replace("%PROPS%", ACCENT_PROP % (660, 430))
(HERE / "Marcas.dc.html").write_text(marcas, encoding="utf-8", newline="\n")

print("escritos: Hibrido.dc.html, Carita.dc.html, Marcas.dc.html")
