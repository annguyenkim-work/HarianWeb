(() => {
  // Keeps the group modal consistent: tier "Từ" follows the previous "Đến", only the last tier is open-ended,
  // and the % matrix (tier x selected product) is rebuilt while preserving typed values.
  let seq = 0;

  const bind = (form) => {
    if (!form || form.dataset.groupBound) return;
    form.dataset.groupBound = '1';
    const tbody = form.querySelector('[data-tiers] tbody');
    const matrix = form.querySelector('[data-matrix]');

    const readValues = () => {
      const values = {};
      matrix.querySelectorAll('[data-cell]').forEach(i => { values[i.dataset.cell] = i.value; });
      return values;
    };

    const rebuild = () => {
      const values = readValues();
      const rows = [...tbody.querySelectorAll('tr[data-tier-key]')];
      let prevMax = 0;
      rows.forEach((row, i) => {
        const isLast = i === rows.length - 1;
        const id = row.querySelector('[data-tier-id]');
        const min = row.querySelector('[data-tier-min]');
        const max = row.querySelector('[data-tier-max]');
        id.name = `Tiers[${i}].Id`;
        id.disabled = !id.value;
        min.name = `Tiers[${i}].MinQuantity`;
        min.value = i === 0 ? 1 : (prevMax ? prevMax + 1 : '');
        max.name = `Tiers[${i}].MaxQuantity`;
        if (isLast) { max.value = ''; max.disabled = true; max.placeholder = 'trở lên'; }
        else { max.disabled = false; max.placeholder = ''; max.required = true; }
        prevMax = parseInt(max.value, 10) || 0;
      });

      const products = [...form.querySelectorAll('[data-products] input[type="checkbox"]:checked')]
        .map(c => ({ id: c.value, name: c.dataset.productName }));

      const table = document.createElement('table');
      table.className = 'data-table discount-table';
      const head = document.createElement('tr');
      head.innerHTML = '<th>Sản phẩm</th>';
      rows.forEach((row) => {
        const th = document.createElement('th');
        const min = row.querySelector('[data-tier-min]').value;
        const max = row.querySelector('[data-tier-max]').value;
        th.textContent = max ? `${min}-${max}` : `Từ ${min}`;
        head.appendChild(th);
      });
      const thead = document.createElement('thead');
      thead.appendChild(head);
      table.appendChild(thead);
      const body = document.createElement('tbody');
      products.forEach(p => {
        const tr = document.createElement('tr');
        const name = document.createElement('td');
        name.textContent = p.name;
        tr.appendChild(name);
        rows.forEach((row, i) => {
          const key = `${row.dataset.tierKey}|${p.id}`;
          const td = document.createElement('td');
          const input = document.createElement('input');
          Object.assign(input, { type: 'number', min: 0, max: 100, step: '0.01', required: true, className: 'form-control' });
          input.style.maxWidth = '6rem';
          input.name = `Tiers[${i}].Percents[${p.id}]`;
          input.dataset.cell = key;
          input.value = values[key] ?? '';
          input.setAttribute('aria-label', `${p.name} ${head.children[i + 1].textContent}`);
          td.appendChild(input);
          tr.appendChild(td);
        });
        body.appendChild(tr);
      });
      table.appendChild(body);
      matrix.replaceChildren(table);
    };

    form.addEventListener('click', e => {
      if (e.target.closest('[data-add-tier]')) {
        const tr = document.createElement('tr');
        tr.dataset.tierKey = `n${++seq}`;
        tr.innerHTML = `
          <td><input type="hidden" data-tier-id value="" /><input type="number" class="form-control" data-tier-min readonly style="max-width:6rem" /></td>
          <td><input type="number" class="form-control" data-tier-max min="1" style="max-width:6rem" /></td>
          <td><button type="button" class="btn-icon btn-icon--danger" data-remove-tier title="Xóa mức" aria-label="Xóa mức"><i class="fa-solid fa-trash" aria-hidden="true"></i></button></td>`;
        tbody.appendChild(tr);
        rebuild();
        return;
      }
      const remove = e.target.closest('[data-remove-tier]');
      if (remove && tbody.querySelectorAll('tr[data-tier-key]').length > 1) {
        remove.closest('tr').remove();
        rebuild();
      }
    });
    form.addEventListener('change', e => {
      if (e.target.matches('[data-tier-max], [data-products] input[type="checkbox"]')) rebuild();
    });
    rebuild();
  };

  window.DealerDiscountGroupForm = { bind };
})();
