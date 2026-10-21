// Inventory AC OEE - panel kanan: Actual (bar ke kiri) vs Plan (bar ke kanan) per model Expander Kyoshin 6.35.
// Periode default satu bulan (bulan filter tabel); bisa diatur lewat filter Dari / Sampai.
// Data: handler PlanActual halaman ini (dbo.PlcKyoshinChangePlan; Plan = Change Plan = bar ungu Production Achievement).
(function () {
    'use strict';

    const $ = (id) => document.getElementById(id);
    const body = $('invPaBody');
    const fromInput = $('invPaFrom'), toInput = $('invPaTo');
    if (!body || !fromInput || !toInput) return;

    const month = window.__invMonth || null;
    const fmt = (n) => Number(n || 0).toLocaleString('id-ID');
    const esc = (s) => String(s ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const iso = (d) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
    const parse = (s) => { const [y, m, d] = s.split('-').map(Number); return new Date(y, m - 1, d); };

    // Hari produksi sekarang (sebelum 07:00 = kemarin)
    const productionToday = () => { const n = new Date(Date.now() - 7 * 3600 * 1000); return new Date(n.getFullYear(), n.getMonth(), n.getDate()); };

    let from, to, request = 0;

    function setRange(f, t) {
        from = f; to = t;
        fromInput.value = iso(from);
        toInput.value = iso(to);
        load();
    }
    const setMonth = (y, m) => setRange(new Date(y, m - 1, 1), new Date(y, m, 0));

    async function load() {
        const my = ++request;
        body.innerHTML = '<div class="inv-runs-empty">Memuat...</div>';
        let data;
        try {
            const q = new URLSearchParams({ handler: 'PlanActual', from: iso(from), to: iso(to) });
            const res = await fetch(`${window.location.pathname}?${q}`, { cache: 'no-store' });
            data = await res.json();
            if (!res.ok) throw new Error(data && data.message ? data.message : `HTTP ${res.status}`);
        } catch (err) {
            if (my !== request) return;
            body.innerHTML = `<div class="inv-runs-empty">Data tidak bisa dimuat: ${esc(err.message)}</div>`;
            return;
        }
        if (my !== request) return;
        // Model yang Actual & Plan-nya sama-sama 0 tidak ditampilkan
        render((data.items || []).filter((x) => (x.actual || 0) !== 0 || (x.plan || 0) !== 0));
    }

    function render(items) {
        if (items.length === 0) {
            body.innerHTML = '<div class="inv-runs-empty">Tidak ada model yang jalan di Expander Kyoshin 6.35 pada periode ini.</div>';
            return;
        }

        // Skala sama untuk kedua sisi supaya panjang bar Actual & Plan bisa dibandingkan langsung
        const max = Math.max(1, ...items.map((x) => Math.max(x.plan || 0, x.actual || 0)));
        const w = (v) => Math.max(0, (v || 0) / max).toFixed(4); // fraksi skala, lebar bar di CSS (--w)
        body.innerHTML =
            '<div class="inv-pa-chart">' +
            items.map((x) => {
                const good = (x.actual || 0) >= (x.plan || 0);
                const tip = `${x.model}: Actual ${fmt(x.actual)} / Plan ${fmt(x.plan)} (${x.days} hari)`;
                return `<div class="inv-pa-row" title="${esc(tip)}">` +
                    `<div class="inv-pa-side inv-pa-left"><span class="inv-pa-val ${good ? 'inv-good' : 'inv-bad'}">${fmt(x.actual)}</span><i class="inv-pa-bar inv-pa-bar-actual" style="--w:${w(x.actual)}"></i></div>` +
                    `<div class="inv-pa-model">${esc(x.model)}</div>` +
                    `<div class="inv-pa-side inv-pa-right"><i class="inv-pa-bar inv-pa-bar-plan" style="--w:${w(x.plan)}"></i><span class="inv-pa-val">${fmt(x.plan)}</span></div>` +
                    '</div>';
            }).join('') +
            '</div>';
    }

    $('invPaFilter').addEventListener('submit', (e) => {
        e.preventDefault();
        if (!fromInput.value || !toInput.value) return;
        let f = parse(fromInput.value), t = parse(toInput.value);
        if (t < f) [f, t] = [t, f];
        setRange(f, t);
    });
    // Awal: satu bulan penuh sesuai bulan tabel (filter Bulan / Tahun di atas)
    const today = productionToday();
    if (month) setMonth(month.year, month.month);
    else setMonth(today.getFullYear(), today.getMonth() + 1);
})();
