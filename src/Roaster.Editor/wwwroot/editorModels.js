window.RoasterModels = {
    _models: {},
    _viewStates: {},

    isEditorReady: function (editorId) {
        try {
            return !!(window.blazorMonaco && window.blazorMonaco.editor && window.blazorMonaco.editor.getEditor(editorId, true));
        } catch {
            return false;
        }
    },

    createModel: function (id, content, language) {
        if (this._models[id]) this._models[id].dispose();
        this._models[id] = monaco.editor.createModel(content || '', language || 'csharp');
    },

    setActiveModel: function (editorId, id) {
        const editor = window.blazorMonaco.editor.getEditor(editorId, true);
        if (!editor) return false;
        const model = this._models[id];
        if (!model) return false;

        const currentModel = editor.getModel();
        if (currentModel) {
            const currentId = Object.keys(this._models).find(k => this._models[k] === currentModel);
            if (currentId) {
                this._viewStates[currentId] = editor.saveViewState();
            } else {
                // Initial model created by BlazorMonaco; not tracked by us — dispose it.
                currentModel.dispose();
            }
        }

        editor.setModel(model);
        if (this._viewStates[id]) editor.restoreViewState(this._viewStates[id]);
        return true;
    },

    disposeModel: function (id) {
        if (this._models[id]) {
            this._models[id].dispose();
            delete this._models[id];
            delete this._viewStates[id];
        }
    },

    getModelValue: function (id) {
        const model = this._models[id];
        return model ? model.getValue() : '';
    },

    getEditorValue: function (editorId) {
        const editor = window.blazorMonaco.editor.getEditor(editorId, true);
        return editor ? editor.getValue() : '';
    },

    setModelLanguage: function (id, language) {
        const model = this._models[id];
        if (model) monaco.editor.setModelLanguage(model, language);
    },

    disposeAll: function () {
        Object.keys(this._models).forEach(id => this._models[id].dispose());
        this._models = {};
        this._viewStates = {};
    }
};
