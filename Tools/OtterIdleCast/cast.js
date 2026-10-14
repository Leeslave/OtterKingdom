// Idle cast: plaza otters in the V14 look (the waiter's line art with a bare head and the
// farmer's eyes, outfits recoloured by make_base.py), posed like QuarterRigView's idle and
// finished with hats and props drawn here. build.py embeds DATA and this script in
// ArtSource/Otter/IdleCast/v14/idle-cast.html; #export=<id> shows one otter on a transparent page.
(function () {
  const canvas = document.getElementById("stage");
  const ctx = canvas.getContext("2d");
  const OUTLINE = "#320a00";
  const LINE = 4.5;   // the V14 parts' outline, in rig px

  function ink(fill, width) {
    ctx.fillStyle = fill; ctx.strokeStyle = OUTLINE; ctx.lineWidth = width || LINE;
    ctx.lineJoin = "round"; ctx.lineCap = "round";
  }
  function shape(fill, draw, width) { ink(fill, width); ctx.beginPath(); draw(); ctx.fill(); ctx.stroke(); }
  function line(color, width, draw) { ctx.strokeStyle = color; ctx.lineWidth = width; ctx.lineCap = "round"; ctx.beginPath(); draw(); ctx.stroke(); }
  function dot(color, x, y, r) { ctx.fillStyle = color; ctx.beginPath(); ctx.arc(x, y, r, 0, Math.PI * 2); ctx.fill(); }

  // ---- hats, in head space (rig px, y down; the head spans x -135..135, y -180..20) ----
  const HATS = {
    beret() {
      ctx.save(); ctx.translate(-14, -150); ctx.rotate(-.2);
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
  };

  // ---- props in a paw (arm space: the shoulder joint at 0, the paw `reach` below it) ----
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
      // Held up and out to the side, the bowl clear of the head.
      ctx.save(); ctx.translate(2, reach * .78); ctx.rotate(-.55);
      shape("#b9824d", () => ctx.roundRect(-4.5, -76, 9, 92, 4.5), 3.5);
      shape("#c48d55", () => ctx.ellipse(0, -90, 17, 23, 0, 0, Math.PI * 2), 3.5);
      shape("#a8733f", () => ctx.ellipse(0, -92, 9, 13, 0, 0, Math.PI * 2), 2);
      ctx.restore();
    },
    magnifier(reach) {
      ctx.save(); ctx.translate(2, reach * .78); ctx.rotate(-.5);
      shape("#7a4526", () => ctx.roundRect(-6, -10, 12, 42, 6), 3.5);
      shape("rgba(205,235,245,.8)", () => ctx.arc(0, -38, 27, 0, Math.PI * 2), 7);
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
      line("rgba(60,40,30,.6)", 2, () => { ctx.moveTo(tip.x, tip.y); ctx.quadraticCurveTo(tip.x + 14, tip.y + 80, tip.x + 6, tip.y + 170); });
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

  const CAST = [
    { id: "painter", name: "화가 해달", hat: "beret", near: "brush", far: "palette" },
    { id: "chef", name: "요리사 해달", hat: "toque", near: "spoon" },
    { id: "explorer", name: "탐험가 해달", hat: "safari", near: "magnifier" },
    { id: "sailor", name: "뱃사람 해달", hat: "sailor", near: "rod" },
    { id: "sleepy", name: "잠꾸러기 해달", hat: "nightcap", near: "starfish", sleepy: true },
  ];

  const images = {};
  function load(key, sprite) {
    return new Promise(resolve => { const im = new Image(); im.onload = () => { images[key] = im; resolve(); }; im.src = sprite.src; });
  }
  function spriteFor(member, part) {
    const own = DATA.outfits[member.id] && DATA.outfits[member.id][part];
    if (own) return { key: member.id + ":" + part, s: own };
    if (part === "head" && member.sleepy) return { key: "head-sleep", s: DATA.parts["head-sleep"] };
    return { key: part, s: DATA.parts[part] };
  }

  // Idle as QuarterRigView poses it (no walk, action or seat): hips settled 8px, a slow breath, a tail sway.
  function drawOtter(member, clock, x, y, scale) {
    const L = DATA.rig.legSolver;
    const world = {};
    const items = [];
    for (const b of DATA.rig.bones) {
      let m;
      if (b.name.endsWith("Leg")) {
        const near = b.name === "nearLeg";
        m = new DOMMatrix().translate(near ? L.nearX : L.farX, near ? 0 : -4);
      } else {
        let by = b.pos[1], angle = 0;
        if (b.name === "hips") by += 8;
        if (b.name === "body") by += Math.sin(clock * 3) * .6;
        if (b.name === "tail") angle = .025 * Math.sin(clock * 2);
        const parent = b.parent ? world[b.parent] : new DOMMatrix();
        m = parent.translate(b.pos[0], by).rotate(angle * 180 / Math.PI);
      }
      world[b.name] = m;
      if (b.name !== "hips") items.push({ b, m });
    }
    items.sort((p, q) => p.b.layer - q.b.layer);
    ctx.save(); ctx.translate(x, y); ctx.scale(scale, scale);
    for (const { b, m } of items) {
      const { key, s } = spriteFor(member, b.name);
      const im = images[key];
      if (!im) continue;
      const w = s.w / 2, h = s.h / 2;
      ctx.save();
      ctx.transform(m.a, m.b, m.c, m.d, m.e, m.f);
      ctx.drawImage(im, -s.pivot[0] * w, -(1 - s.pivot[1]) * h, w, h);
      const reach = s.pivot[1] * h;   // shoulder joint to the bottom of the arm sprite
      if (b.name === "head" && member.hat) HATS[member.hat]();
      if (b.name === "nearArm" && member.near) PROPS[member.near](reach);
      if (b.name === "farArm" && member.far) PROPS[member.far](reach);
      ctx.restore();
    }
    ctx.restore();
  }

  function gallery(clock) {
    const W = canvas.width, H = canvas.height;
    ctx.setTransform(1, 0, 0, 1, 0, 0);
    ctx.clearRect(0, 0, W, H);
    const step = W / CAST.length;
    const style = getComputedStyle(document.documentElement);
    CAST.forEach((member, i) => {
      const x = step * (i + .5), y = H * .8;
      ctx.fillStyle = style.getPropertyValue("--shadow");
      ctx.beginPath(); ctx.ellipse(x, y + 2, step * .26, step * .05, 0, 0, Math.PI * 2); ctx.fill();
      drawOtter(member, clock + i * .7, x, y, Math.min(step / 320, H / 420));
      ctx.fillStyle = style.getPropertyValue("--ink");
      ctx.font = `${Math.round(Math.min(step / 10, 26))}px "Jua", system-ui, sans-serif`;
      ctx.textAlign = "center";
      ctx.fillText(member.name, x, y + Math.min(step / 320, H / 420) * 60);
    });
  }

  const query = new URLSearchParams(location.hash.slice(1));
  const exportId = query.get("export");
  Promise.all([
    ...Object.entries(DATA.parts).map(([k, s]) => load(k, s)),
    ...Object.entries(DATA.outfits).flatMap(([id, parts]) => Object.entries(parts).map(([k, s]) => load(id + ":" + k, s))),
  ]).then(() => {
    if (exportId) {
      document.documentElement.classList.add("export");
      canvas.width = 900; canvas.height = 1000;
      ctx.clearRect(0, 0, 900, 1000);
      drawOtter(CAST.find(m => m.id === exportId), 0, 450, 930, +(query.get("scale") || 2.15));
      document.title = "ready";
      return;
    }
    const fit = () => {
      const r = canvas.getBoundingClientRect(), dpr = Math.min(devicePixelRatio || 1, 2);
      canvas.width = Math.round(r.width * dpr); canvas.height = Math.round(r.height * dpr);
    };
    fit();
    addEventListener("resize", fit);
    const still = matchMedia("(prefers-reduced-motion: reduce)").matches;
    const start = performance.now();
    const frame = now => { gallery(still ? 0 : (now - start) / 1000); if (!still) requestAnimationFrame(frame); };
    requestAnimationFrame(frame);
  });
})();
