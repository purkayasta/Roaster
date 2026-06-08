window.RoasterReview = {
    // zoneId -> { editor, node, parking, zone }
    _zones: {},

    // Add a Monaco view zone that hosts the real comment DOM node.
    // Monaco reparents `node` into its own .view-zones container and manages
    // its position/scroll natively — no overlay or manual sync required.
    addZone: function (editorId, lineNumber, node) {
        const editor = window.blazorMonaco.editor.getEditor(editorId, true);
        if (!editor || !node) return null;

        // Capture the Blazor-owned parent BEFORE Monaco moves the node, so we
        // can restore it on removal and keep Blazor's DOM removal valid.
        const parking = node.parentElement;

        const zone = {
            afterLineNumber: lineNumber,
            domNode: node,
            heightInPx: node.offsetHeight || 120
        };

        let zoneId = null;
        editor.changeViewZones(accessor => {
            zoneId = accessor.addZone(zone);
        });
        if (zoneId === null) return null;

        this._zones[zoneId] = { editor, node, parking, zone };
        this._relayout(zoneId);   // node now has real content width — re-measure
        return zoneId;
    },

    // Re-measure the hosted node and resize its zone to fit.
    updateZoneHeight: function (editorId, zoneId) {
        this._relayout(zoneId);
    },

    _relayout: function (zoneId) {
        const entry = this._zones[zoneId];
        if (!entry) return;
        requestAnimationFrame(() => {
            if (!this._zones[zoneId]) return;
            const h = entry.node.offsetHeight;
            if (h && h !== entry.zone.heightInPx) {
                entry.zone.heightInPx = h;
                entry.editor.changeViewZones(accessor => accessor.layoutZone(zoneId));
            }
        });
    },

    removeZone: function (editorId, zoneId) {
        const entry = this._zones[zoneId];
        if (!entry) return;
        entry.editor.changeViewZones(accessor => accessor.removeZone(zoneId));
        // Return the node to its Blazor-owned parent so Blazor can dispose it
        // without hitting a removeChild error on a node it no longer "contains".
        if (entry.parking && entry.node) entry.parking.appendChild(entry.node);
        delete this._zones[zoneId];
    },

    removeZones: function (editorId, zoneIds) {
        if (!zoneIds) return;
        zoneIds.forEach(id => this.removeZone(editorId, id));
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
