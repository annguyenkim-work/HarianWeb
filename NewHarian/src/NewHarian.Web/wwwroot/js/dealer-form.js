(() => {
  // Shared by the guest register form and the admin dealer modal. The server re-applies the same
  // rules (DealerProfileValidator.Normalize), so this only keeps the UI in sync.
  const sync = (form) => {
    const checked = form.querySelector('[name="BusinessType"]:checked');
    const type = checked ? checked.value : (form.querySelector('[name="BusinessType"]')?.value || 'Company');

    form.querySelectorAll('[data-dealer-only]').forEach(el => {
      const on = el.dataset.dealerOnly === type;
      el.hidden = !on;
      el.querySelectorAll('input, select, textarea').forEach(i => { i.disabled = !on; });
    });
    form.querySelectorAll('[data-dealer-text]').forEach(el => {
      el.hidden = el.dataset.dealerText !== type;
    });
    form.querySelectorAll('[data-required-for]').forEach(el => {
      el.required = el.dataset.requiredFor === type;
    });

    const ship = form.querySelector('[name="ShipToHeadOffice"][type="checkbox"]');
    const shipBlock = form.querySelector('[data-dealer-shipping]');
    if (ship && shipBlock) {
      shipBlock.hidden = ship.checked;
      shipBlock.disabled = ship.checked;
    }
  };

  const bind = (form) => {
    if (form.dataset.dealerFormBound) return;
    form.dataset.dealerFormBound = '1';
    form.addEventListener('change', e => {
      if (e.target.matches('[name="BusinessType"], [name="ShipToHeadOffice"]')) sync(form);
    });
    sync(form);
  };

  const bindAll = () => document.querySelectorAll('[data-dealer-form]').forEach(bind);
  window.HarianDealerForm = { bindAll };
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', bindAll);
  else bindAll();
})();
