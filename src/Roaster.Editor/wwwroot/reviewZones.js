window.RoasterReview = {
    _zoneConfigs: {},
    _overlaySubscribed: false,

    addZone: function (editorId, lineNumber, domNode) {
        const editor = window.blazorMonaco.editor.getEditor(editorId, true);
        if (!editor) return null;

        // Use an invisible spacer as the zone node — never put Blazor components into Monaco's DOM.
        const spacer = document.createElement('div');
        const height = domNode.scrollHeight || 120;

        const config = {
            afterLineNumber: lineNumber,
            domNode: spacer,
            heightInPx: height,
            suppressMouseDown: true
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

        const config = this._zoneConfigs[zoneId];
        if (config) config.heightInPx = domNode.scrollHeight || 120;

        editor.changeViewZones(accessor => {
            accessor.layoutZone(zoneId);
        });
    },

    syncOverlay: function (editorId, overlayId) {
        const editor = window.blazorMonaco.editor.getEditor(editorId, true);
        if (!editor) return;

        const self = this;
        const update = () => {
            const overlay = document.getElementById(overlayId);
            if (!overlay) return;
            const scrollTop = editor.getScrollTop();
            const contentLeft = editor.getLayoutInfo().contentLeft;

            overlay.querySelectorAll('[data-line]').forEach(el => {
                const lineNum = parseInt(el.dataset.line);
                // Use the stored spacer height so top is always pinned to the top of the zone,
                // regardless of whether the comment element itself is collapsed or expanded.
                const config = Object.values(self._zoneConfigs).find(c => c.afterLineNumber === lineNum);
                const zoneHeight = config ? config.heightInPx : (el.scrollHeight || 120);
                const top = editor.getTopForLineNumber(lineNum + 1) - zoneHeight - scrollTop;
                el.style.top  = top + 'px';
                el.style.left = contentLeft + 'px';
            });

            overlay.style.visibility = 'visible';
        };

        if (!this._overlaySubscribed) {
            editor.onDidScrollChange(update);
            editor.onDidLayoutChange(update);
            this._overlaySubscribed = true;
        }

        // Attach wheel forwarding to the overlay element itself. Use a flag on the
        // element so re-calling syncOverlay (e.g. after a new review loads) re-binds
        // if the element was recreated by Blazor.
        const overlay = document.getElementById(overlayId);
        if (overlay && !overlay._wheelBound) {
            overlay.addEventListener('wheel', (e) => {
                e.preventDefault();
                editor.setScrollTop(editor.getScrollTop() + e.deltaY);
                editor.setScrollLeft(editor.getScrollLeft() + e.deltaX);
            }, { passive: false });
            overlay._wheelBound = true;
        }

        update();
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
