/* Shared TinyMCE mount. Height lives in admin.css on .richtext-editor. */
(() => {
  function mark(editor) {
    editor.getContainer()?.classList.add('richtext-editor');
  }

  window.AdminRichText = {
    init(textareas, options) {
      if (!window.tinymce) return;
      const extra = options || {};
      const userCallback = extra.init_instance_callback;
      [...textareas].forEach((ta) => {
        if (!ta || ta.closest('[hidden]') || tinymce.get(ta.id)) return;
        tinymce.init(Object.assign({}, extra, {
          target: ta,
          init_instance_callback(editor) {
            mark(editor);
            if (typeof userCallback === 'function') userCallback(editor);
          }
        }));
      });
    }
  };
})();
