window.RoasterReview = {
    // Mutable zone config objects keyed by zoneId — required to resize via layoutZone.
    _zoneConfigs: {},

    addZone: function (editorId, lineNumber, domNode) {
        const editor = window.blazorMonaco.editor.getEditor(editorId, true);
        if (!editor) return null;

        domNode.style.overflow = 'hidden';

        const config = {
            afterLineNumber: lineNumber,
            domNode: domNode,
            heightInPx: domNode.scrollHeight || 120,
            suppressMouseFromEvent: (e) => !e.target.element.closest('input, textarea, button, [role="button"]')
        };

        let zoneId = null;
        editor.changeViewZones(accessor => {
            zoneId = accessor.addZone(config);
        });

        if (zoneId !== null)
            this._zoneConfigs[zoneId] = config;

        return zoneId;
    },

    updateZoneHeight: function (editorId, zoneId, domNode) {
        const editor = window.blazorMonaco.editor.getEditor(editorId, true);
        if (!editor || zoneId === null) return;

        // Mutate the stored config — Monaco reads heightInPx from it on layoutZone.
        const config = this._zoneConfigs[zoneId];
        if (config) config.heightInPx = domNode.scrollHeight || 120;

        editor.changeViewZones(accessor => {
            accessor.layoutZone(zoneId);
        });
    },

    removeZones: function (editorId, zoneIds) {
        const editor = window.blazorMonaco.editor.getEditor(editorId, true);
        if (!editor || !zoneIds || zoneIds.length === 0) return;

        editor.changeViewZones(accessor => {
            zoneIds.forEach(id => {
                accessor.removeZone(id);
                delete this._zoneConfigs[id];
            });
        });
    },

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
