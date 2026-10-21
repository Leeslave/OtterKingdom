// Hats, outfits and held props for the cast rigs, drawn onto each part (front and rear) so they
// become part of its sprite. build.py lays every part of one otter out in a grid of CELL x CELL
// cells, the part's joint at each cell's centre, at 2 image px per rig px (PPU 200, like the V14
// parts), and screenshots the page; ?member=<id>.
(function () {
  const CELL = 1200;
  const COLS = 5;
  const canvas = document.getElementById("atlas");
  const ctx = canvas.getContext("2d");
  const OUTLINE = "#320a00";
  const LINE = 4.5;

  function ink(fill, width) {
    ctx.fillStyle = fill; ctx.strokeStyle = OUTLINE; ctx.lineWidth = width || LINE;
    ctx.lineJoin = "round"; ctx.lineCap = "round";
  }
  function shape(fill, draw, width) { ink(fill, width); ctx.beginPath(); draw(); ctx.fill(); ctx.stroke(); }
  function line(color, width, draw) { ctx.strokeStyle = color; ctx.lineWidth = width; ctx.lineCap = "round"; ctx.beginPath(); draw(); ctx.stroke(); }
  function dot(color, x, y, r) { ctx.fillStyle = color; ctx.beginPath(); ctx.arc(x, y, r, 0, Math.PI * 2); ctx.fill(); }

  // ---- hats (head space, rig px: the waiter's head spanned x -135..135, y -180..20; the farmer's
  // bare head the cast wears is lower and narrower, so hats move down HAT_DROP) ----
  const HAT_DROP = 42;
  const HATS = {
    beret(rear) {
      ctx.save(); ctx.translate(rear ? 10 : -14, -150); ctx.rotate(rear ? .16 : -.2);
      shape("#2b2622", () => ctx.ellipse(0, 0, 92, 34, 0, 0, Math.PI * 2));
      shape("#2b2622", () => { ctx.moveTo(-6, -30); ctx.quadraticCurveTo(2, -50, 12, -32); });
      line("rgba(255,255,255,.16)", 6, () => ctx.ellipse(-16, -8, 58, 15, 0, Math.PI * 1.1, Math.PI * 1.6));
      ctx.restore();
    },
    toque() {
      ctx.save(); ctx.translate(2, -146); ctx.rotate(.04); ctx.scale(.86, .86);
      shape("#fdfbf6", () => {
        ctx.moveTo(-62, 4);
        ctx.bezierCurveTo(-112, -6, -104, -86, -54, -78);
        ctx.bezierCurveTo(-48, -128, 22, -132, 30, -92);
        ctx.bezierCurveTo(70, -116, 120, -70, 70, 4);
        ctx.closePath();
      });
      line("#e6e0d2", 5, () => { ctx.moveTo(-30, -60); ctx.quadraticCurveTo(-26, -30, -32, -6); ctx.moveTo(22, -70); ctx.quadraticCurveTo(28, -36, 24, -6); });
      shape("#fdfbf6", () => ctx.roundRect(-72, -12, 148, 36, 14));
      ctx.restore();
    },
    safari() {
      ctx.save(); ctx.translate(2, -138); ctx.rotate(-.06);
      shape("#e9b23a", () => ctx.ellipse(4, 14, 148, 40, 0, 0, Math.PI * 2));
      shape("#f2be43", () => { ctx.moveTo(-96, 12); ctx.bezierCurveTo(-100, -78, 104, -84, 100, 6); ctx.quadraticCurveTo(0, 30, -96, 12); });
      shape("#7a4526", () => { ctx.moveTo(-97, -6); ctx.quadraticCurveTo(0, 18, 99, -12); ctx.lineTo(100, 6); ctx.quadraticCurveTo(0, 32, -96, 12); ctx.closePath(); });
      ctx.restore();
    },
    sailor() {
      ctx.save(); ctx.translate(2, -146); ctx.rotate(-.1);
      shape("#fbf9f4", () => { ctx.moveTo(-78, 8); ctx.bezierCurveTo(-82, -62, 84, -66, 80, 6); ctx.closePath(); });
      shape("#2f5fa7", () => ctx.roundRect(-92, -6, 186, 30, 14));
      line("#ffffff", 4, () => { ctx.moveTo(-80, 9); ctx.lineTo(82, 7); });
      ctx.restore();
    },
    nightcap() {
      ctx.save(); ctx.translate(-6, -144); ctx.rotate(-.1); ctx.scale(.9, .9);
      shape("#6f9de0", () => {
        ctx.moveTo(-96, 10);
        ctx.bezierCurveTo(-90, -70, 30, -96, 92, -44);
        ctx.bezierCurveTo(124, -20, 138, 24, 132, 78);
        ctx.quadraticCurveTo(116, 34, 98, 8);
        ctx.closePath();
      });
      line("rgba(255,255,255,.28)", 6, () => { ctx.moveTo(-40, -40); ctx.quadraticCurveTo(30, -70, 88, -38); });
      shape("#f6f3ec", () => ctx.roundRect(-104, -4, 210, 32, 16));
      shape("#f6f3ec", () => ctx.arc(132, 88, 20, 0, Math.PI * 2));
      ctx.restore();
    },
    jester(rear) {
      // A yellow band and three floppy points (green, red, red) with blue bells at the tips.
      ctx.save(); ctx.translate(0, -146); ctx.scale(rear ? -1 : 1, 1);
      const bell = (x, y) => shape("#5d97cf", () => ctx.arc(x, y, 15, 0, Math.PI * 2), 4);
      shape("#90ba54", () => { ctx.moveTo(-88, 2); ctx.bezierCurveTo(-110, -60, -120, -92, -112, -112); ctx.bezierCurveTo(-82, -82, -46, -50, -18, -6); ctx.closePath(); });
      shape("#db4e48", () => { ctx.moveTo(-34, 0); ctx.bezierCurveTo(-40, -70, -10, -112, 24, -122); ctx.bezierCurveTo(40, -90, 48, -50, 40, -4); ctx.closePath(); });
      shape("#db4e48", () => { ctx.moveTo(10, 0); ctx.bezierCurveTo(30, -76, 100, -96, 150, -58); ctx.bezierCurveTo(160, -48, 164, -30, 160, -16); ctx.bezierCurveTo(130, -44, 100, -40, 86, -2); ctx.closePath(); });
      line("rgba(255,255,255,.25)", 6, () => { ctx.moveTo(-2, -60); ctx.quadraticCurveTo(10, -96, 22, -108); ctx.moveTo(50, -40); ctx.quadraticCurveTo(90, -70, 130, -60); });
      bell(-112, -112); bell(24, -124); bell(160, -14);
      shape("#ecc85c", () => ctx.roundRect(-98, -14, 196, 30, 14));
      for (const x of [-60, -20, 20, 60]) shape("#db4e48", () => ctx.arc(x, 1, 5, 0, Math.PI * 2), 3);
      ctx.restore();
    },
  };

  // ---- outfits drawn over the body (body space: x -75..75, y -71..48) ----
  const OUTFITS = {
    apron(rear) {
      if (rear) {
        // Strings tied in a bow at the back of the waist.
        line(OUTLINE, 9, () => { ctx.moveTo(-74, 10); ctx.quadraticCurveTo(0, 22, 74, 8); });
        line("#fdfbf6", 5, () => { ctx.moveTo(-72, 10); ctx.quadraticCurveTo(0, 22, 72, 8); });
        shape("#fdfbf6", () => { ctx.moveTo(0, 16); ctx.lineTo(-20, 4); ctx.lineTo(-22, 28); ctx.closePath(); }, 3.5);
        shape("#fdfbf6", () => { ctx.moveTo(0, 16); ctx.lineTo(20, 4); ctx.lineTo(22, 28); ctx.closePath(); }, 3.5);
        shape("#fdfbf6", () => ctx.arc(0, 16, 6, 0, Math.PI * 2), 3.5);
        return;
      }
      shape("#fdfbf6", () => {
        ctx.moveTo(-30, -46); ctx.lineTo(28, -48); ctx.lineTo(32, -2);
        ctx.quadraticCurveTo(54, 10, 52, 46); ctx.quadraticCurveTo(0, 58, -54, 44);
        ctx.quadraticCurveTo(-56, 8, -34, -2); ctx.closePath();
      });
      line(OUTLINE, 3.5, () => { ctx.moveTo(-30, -46); ctx.lineTo(-46, -70); ctx.moveTo(28, -48); ctx.lineTo(40, -72); });
      shape("#f1e7d4", () => ctx.roundRect(-15, 12, 30, 20, 6), 3);
    },
    backpack(rear) {
      if (!rear) {
        for (const x of [-34, 30]) shape("#7a4526", () => ctx.roundRect(x - 5, -66, 10, 60, 5), 3.5);
        return;
      }
      shape("#9a6a3c", () => ctx.roundRect(-46, -58, 92, 86, 18));
      shape("#875a30", () => { ctx.moveTo(-46, -36); ctx.quadraticCurveTo(0, -60, 46, -36); ctx.lineTo(46, -14); ctx.quadraticCurveTo(0, -30, -46, -14); ctx.closePath(); });
      shape("#f2c14b", () => ctx.roundRect(-7, -22, 14, 12, 3), 3);
      shape("#875a30", () => ctx.roundRect(-30, 0, 60, 22, 7), 3.5);
    },
    ruff(rear) {
      // A frilled collar of green, yellow and red scallops around the neck; buttons down the front.
      const colours = ["#90ba54", "#ecc85c", "#db4e48"];
      for (let i = 0; i <= 8; i++) {
        const a = Math.PI * i / 8;
        shape(colours[i % 3], () => ctx.ellipse(Math.cos(a) * -60, -64 + Math.sin(a) * 14, 20, 15, Math.PI / 2 - a, 0, Math.PI * 2), 4);
      }
      if (rear) return;
      for (const [y, c] of [[-28, "#90ba54"], [0, "#ecc85c"], [26, "#90ba54"]]) shape(c, () => ctx.arc(-12, y, 7, 0, Math.PI * 2), 3.5);
    },
    collar(rear) {
      if (!rear) return;
      // The sailor collar's square flap down the back.
      shape("#2f5fa7", () => { ctx.moveTo(-52, -70); ctx.lineTo(52, -70); ctx.lineTo(46, -22); ctx.lineTo(-46, -22); ctx.closePath(); });
      line("#ffffff", 4, () => { ctx.moveTo(-42, -32); ctx.lineTo(42, -32); });
    },
  };

  // ---- props in a paw (arm space: shoulder joint at 0, the paw `reach` rig px below it) ----
  const PROPS = {
    brush(reach) {
      ctx.save(); ctx.translate(2, reach * .78); ctx.rotate(-.9);
      shape("#b9824d", () => ctx.roundRect(-4, -58, 8, 76, 4), 3.5);
      shape("#c9c2b8", () => ctx.roundRect(-5, -66, 10, 12, 3), 3.5);
      shape("#e46b5c", () => { ctx.moveTo(-5, -66); ctx.quadraticCurveTo(0, -86, 5, -66); ctx.closePath(); }, 3.5);
      ctx.restore();
    },
    palette(reach) {
      ctx.save(); ctx.translate(12, reach * .74); ctx.rotate(.2);
      shape("#e3b781", () => ctx.ellipse(0, 0, 38, 27, 0, 0, Math.PI * 2));
      for (const [x, y, c] of [[-15, -10, "#e46b5c"], [1, -15, "#f2c14b"], [17, -6, "#4f8ad6"], [15, 10, "#7cb55a"]]) dot(c, x, y, 6.5);
      ctx.restore();
    },
    spoon(reach) {
      ctx.save(); ctx.translate(2, reach * .78); ctx.rotate(-.55);
      shape("#b9824d", () => ctx.roundRect(-4.5, -76, 9, 92, 4.5), 3.5);
      shape("#c48d55", () => ctx.ellipse(0, -90, 17, 23, 0, 0, Math.PI * 2), 3.5);
      shape("#a8733f", () => ctx.ellipse(0, -92, 9, 13, 0, 0, Math.PI * 2), 2);
      ctx.restore();
    },
    magnifier(reach) {
      ctx.save(); ctx.translate(2, reach * .78); ctx.rotate(-.5);
      shape("#7a4526", () => ctx.roundRect(-6, -10, 12, 42, 6), 3.5);
      shape("rgba(205,235,245,.85)", () => ctx.arc(0, -38, 27, 0, Math.PI * 2), 7);
      line("rgba(255,255,255,.85)", 4.5, () => ctx.arc(0, -38, 16, Math.PI * 1.1, Math.PI * 1.45));
      ctx.restore();
    },
    rod(reach) {
      ctx.save(); ctx.translate(2, reach * .78); ctx.rotate(.95);
      line(OUTLINE, 10, () => { ctx.moveTo(0, 30); ctx.lineTo(0, -230); });
      line("#b9824d", 5.5, () => { ctx.moveTo(0, 30); ctx.lineTo(0, -230); });
      shape("#7a8a96", () => ctx.arc(-10, 8, 9, 0, Math.PI * 2), 3.5);
      ctx.rotate(-.95);
      const tip = { x: Math.sin(.95) * 230, y: -Math.cos(.95) * 230 };
      line("rgba(60,40,30,.75)", 2.4, () => { ctx.moveTo(tip.x, tip.y); ctx.quadraticCurveTo(tip.x + 14, tip.y + 80, tip.x + 6, tip.y + 170); });
      ctx.restore();
    },
    starfish(reach) {
      ctx.save(); ctx.translate(14, reach * .66); ctx.rotate(.3); ctx.scale(1.35, 1.35);
      shape("#f39a3d", () => {
        for (let i = 0; i < 10; i++) {
          const r = i % 2 ? 11 : 27, a = -Math.PI / 2 + i * Math.PI / 5;
          ctx[i ? "lineTo" : "moveTo"](Math.cos(a) * r, Math.sin(a) * r);
        }
        ctx.closePath();
      }, 3.5);
      for (let i = 0; i < 5; i++) { const a = -Math.PI / 2 + i * Math.PI * 2 / 5; dot("#ffd08a", Math.cos(a) * 14, Math.sin(a) * 14, 2.6); }
      ctx.restore();
    },
  };

  const CAST = {
    painter: { hat: "beret", near: "brush", far: "palette" },
    explorer: { hat: "safari", outfit: "backpack", near: "magnifier" },
    sleepy: { hat: "nightcap", near: "starfish" },
    clown: { hat: "jester", outfit: "ruff" },
  };

  const member = new URLSearchParams(location.search).get("member");
  const look = CAST[member];
  const cells = DATA.cells;   // [{name, view, part, src, w, h, pivot}]
  canvas.width = COLS * CELL;
  canvas.height = Math.ceil(cells.length / COLS) * CELL;
  canvas.style.width = canvas.width + "px";
  canvas.style.height = canvas.height + "px";

  Promise.all(cells.map(c => new Promise(r => { const im = new Image(); im.onload = () => { c.image = im; r(); }; im.src = c.src; }))).then(() => {
    cells.forEach((c, i) => {
      const cx = (i % COLS) * CELL + CELL / 2, cy = Math.floor(i / COLS) * CELL + CELL / 2;
      ctx.setTransform(1, 0, 0, 1, cx, cy);
      ctx.drawImage(c.image, -c.pivot[0] * c.w, -(1 - c.pivot[1]) * c.h);
      ctx.scale(2, 2);   // costume drawings are in rig px
      const rear = c.view === "rear";
      const reach = c.pivot[1] * c.h / 2;
      // The sleeping head (shown while dozing) wears the same hat.
      if ((c.part === "head" || c.part === "head-sleep") && look.hat) { ctx.translate(0, HAT_DROP); HATS[look.hat](rear); }
      if (c.part === "body" && look.outfit) OUTFITS[look.outfit](rear);
      if (c.part === "nearArm" && look.near) PROPS[look.near](reach);
      if (c.part === "farArm" && look.far) PROPS[look.far](reach);
    });
    document.title = "ready";
  });
})();
