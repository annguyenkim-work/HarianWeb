(() => {
  const dataUrl = '/data/vietnam-divisions.json';
  let cache = null;

  const load = async () => {
    if (cache) return cache;
    const res = await fetch(dataUrl);
    cache = await res.json();
    return cache;
  };

  const fillCommunes = (select, communes, selected) => {
    select.innerHTML = '<option value="">-</option>';
    communes
      .slice()
      .sort((a, b) => a.name.localeCompare(b.name, 'vi'))
      .forEach(c => {
        const opt = document.createElement('option');
        opt.value = c.idCommune;
        opt.textContent = c.name;
        if (selected && selected === c.idCommune) opt.selected = true;
        select.appendChild(opt);
      });
    select.disabled = communes.length === 0;
  };

  const bind = async (root) => {
    const data = await load();
    const province = root.querySelector('[data-address-province]');
    const commune = root.querySelector('[data-address-commune]');
    if (!province || !commune) return;

    const savedProvince = root.querySelector('[data-address-province-value]')?.value
      || province.getAttribute('data-selected')
      || '';
    const savedCommune = commune.getAttribute('data-selected') || '';

    province.innerHTML = '<option value="">-</option>';
    data.province.forEach(p => {
      const opt = document.createElement('option');
      opt.value = p.idProvince;
      opt.textContent = p.name;
      if (savedProvince === p.idProvince) opt.selected = true;
      province.appendChild(opt);
    });

    const communesFor = (code) => data.commune.filter(c => c.idProvince === code);
    if (savedProvince) fillCommunes(commune, communesFor(savedProvince), savedCommune);
    else commune.disabled = true;

    province.addEventListener('change', () => {
      fillCommunes(commune, communesFor(province.value), '');
    });
    root.dispatchEvent(new Event('harian:address-ready', { bubbles: true }));
  };

  const init = () => document.querySelectorAll('[data-address-root]').forEach(el => {
    if (el.dataset.addressBound) return;
    el.dataset.addressBound = '1';
    bind(el);
  });
  window.HarianAddress = { bindAll: init };
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
  else init();
})();
