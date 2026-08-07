(function () {
  function loc(obj, lang) {
    if (!obj || typeof obj !== 'object') return '';
    return obj[lang] || '';
  }

  function escapeAttr(s) {
    return String(s == null ? '' : s)
      .replace(/&/g, '&amp;')
      .replace(/"/g, '&quot;')
      .replace(/</g, '&lt;');
  }

  function emptyRow(id) {
    return {
      id: id || ('row-' + Date.now()),
      sortOrder: 1,
      label: { vi: '', en: '', ja: '' },
      value: { vi: '', en: '', ja: '' }
    };
  }

  window.initCmsTableEditor = function (options) {
    var list = document.getElementById(options.listId || 'table-rows');
    var extra = document.getElementById(options.extraId || 'ExtraData');
    var form = document.getElementById(options.formId || 'block-form');
    var addBtn = document.getElementById(options.addBtnId || 'btn-add-row');
    if (!list || !extra) return;

    var rows = [];
    try {
      var parsed = typeof options.initialJson === 'string'
        ? JSON.parse(options.initialJson)
        : (options.initialJson || {});
      rows = Array.isArray(parsed.rows) ? parsed.rows : [];
    } catch (e) {
      rows = [];
    }
    if (rows.length === 0) rows.push(emptyRow('row-1'));

    function syncFromDom() {
      list.querySelectorAll('input[data-f]').forEach(function (inp) {
        var i = +inp.getAttribute('data-i');
        var parts = (inp.getAttribute('data-f') || '').split('.');
        var group = parts[0];
        var lang = parts[1];
        if (!rows[i][group] || typeof rows[i][group] !== 'object') {
          rows[i][group] = { vi: '', en: '', ja: '' };
        }
        rows[i][group][lang] = inp.value;
      });
    }

    function writeExtra() {
      syncFromDom();
      rows.forEach(function (r, i) {
        r.sortOrder = i + 1;
        if (!r.id) r.id = 'row-' + (i + 1) + '-' + Date.now();
      });
      extra.value = JSON.stringify({ rows: rows });
    }

    function swap(a, b) {
      var tmp = rows[a];
      rows[a] = rows[b];
      rows[b] = tmp;
    }

    function render() {
      list.innerHTML = '';
      rows.forEach(function (row, idx) {
        var card = document.createElement('div');
        card.className = 'table-row-card';

        var head = document.createElement('div');
        head.className = 'table-row-card__head';
        head.innerHTML = '<strong>Dòng ' + (idx + 1) + '</strong>';

        var actions = document.createElement('div');
        actions.className = 'actions';

        var up = document.createElement('button');
        up.type = 'button';
        up.className = 'btn-link';
        up.textContent = '↑';
        up.disabled = idx === 0;
        up.addEventListener('click', function () {
          syncFromDom();
          swap(idx - 1, idx);
          render();
        });

        var down = document.createElement('button');
        down.type = 'button';
        down.className = 'btn-link';
        down.textContent = '↓';
        down.disabled = idx === rows.length - 1;
        down.addEventListener('click', function () {
          syncFromDom();
          swap(idx, idx + 1);
          render();
        });

        var del = document.createElement('button');
        del.type = 'button';
        del.className = 'btn-link';
        del.textContent = 'Xóa';
        del.addEventListener('click', function () {
          syncFromDom();
          rows.splice(idx, 1);
          if (rows.length === 0) rows.push(emptyRow('row-1'));
          render();
        });

        actions.appendChild(up);
        actions.appendChild(down);
        actions.appendChild(del);
        head.appendChild(actions);

        var grid = document.createElement('div');
        grid.className = 'table-row-card__grid';

        var fields = [
          { f: 'label.vi', label: 'Nhãn (VI) *' },
          { f: 'value.vi', label: 'Nội dung (VI) *' },
          { f: 'label.en', label: 'Label (EN)' },
          { f: 'value.en', label: 'Value (EN)' },
          { f: 'label.ja', label: 'ラベル (JA)' },
          { f: 'value.ja', label: '内容 (JA)' }
        ];

        fields.forEach(function (field) {
          var parts = field.f.split('.');
          var group = parts[0];
          var lang = parts[1];
          var lab = document.createElement('label');
          lab.appendChild(document.createTextNode(field.label + ' '));
          var input = document.createElement('input');
          input.className = 'form-control';
          input.setAttribute('data-f', field.f);
          input.setAttribute('data-i', String(idx));
          input.value = loc(row[group], lang);
          lab.appendChild(input);
          grid.appendChild(lab);
        });

        card.appendChild(head);
        card.appendChild(grid);
        list.appendChild(card);
      });
    }

    if (addBtn) {
      addBtn.addEventListener('click', function () {
        syncFromDom();
        rows.push(emptyRow());
        render();
      });
    }

    if (form) form.addEventListener('submit', writeExtra);
    render();
  };
})();
