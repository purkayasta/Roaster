window.RoasterReview = {
    addDecorations: function (editorId, lineNumbers) {
        const editor = window.blazorMonaco.editor.getEditor(editorId, true);
        if (!editor || !lineNumbers || lineNumbers.length === 0) return [];

        const decorations = lineNumbers.map(item => ({
            range: new monaco.Range(item.lineNumber, 1, item.lineNumber, 1),
            options: {
                isWholeLine: true,
                glyphMarginClassName: 'roaster-glyph-' + item.severity.toLowerCase(),
                className: 'roaster-line-' + item.severity.toLowerCase()
            }
        }));

        return editor.deltaDecorations([], decorations);
    },

    clearDecorations: function (editorId, decorationIds) {
        const editor = window.blazorMonaco.editor.getEditor(editorId, true);
        if (!editor || !decorationIds || decorationIds.length === 0) return;

        editor.deltaDecorations(decorationIds, []);
    }
};

// Drag-to-resize for the comment panel. Adjusts the panel's width directly so
// dragging stays smooth without round-tripping through Blazor; Monaco's
// AutomaticLayout ResizeObserver reflows the editor as the panel shrinks/grows.
window.RoasterSplit = {
    attach: function (handle, panel, min, max) {
        if (!handle || !panel || handle.dataset.roasterSplit) return;
        handle.dataset.roasterSplit = '1';

        let startX = 0;
        let startW = 0;

        const onMove = e => {
            const next = Math.max(min, Math.min(max, startW + (startX - e.clientX)));
            panel.style.width = next + 'px';
        };
        const onUp = () => {
            window.removeEventListener('pointermove', onMove);
            window.removeEventListener('pointerup', onUp);
            document.body.style.userSelect = '';
        };

        handle.addEventListener('pointerdown', e => {
            startX = e.clientX;
            startW = panel.offsetWidth;
            document.body.style.userSelect = 'none';
            window.addEventListener('pointermove', onMove);
            window.addEventListener('pointerup', onUp);
            e.preventDefault();
        });
    }
};
