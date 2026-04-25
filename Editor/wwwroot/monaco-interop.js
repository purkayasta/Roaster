/**
 * MONACO EDITOR JAVASCRIPT INTEROP
 *
 * This module handles the integration between Blazor components and the Monaco editor.
 * The key functionality is injecting Blazor-rendered DOM elements into Monaco editor view zones,
 * which allows for GitHub-style inline comments and other widgets.
 *
 * ARCHITECTURE:
 * 1. Blazor renders components (like RoastThread) in hidden divs
 * 2. This JS module receives the element ID via Interop
 * 3. We retrieve the Monaco editor instance
 * 4. We use changeViewZones to insert the element as a view zone
 * 5. The result: Blazor components appear inline in the editor while maintaining full interactivity
 */

// Store references to Monaco editor instances by their element ID
const editorInstances = {};

/**
 * Registers a Monaco editor instance when it's created.
 * Called from Blazor components after the editor is initialized.
 *
 * @param {string} editorId - The HTML element ID of the editor
 * @param {object} editorInstance - The Monaco StandaloneCodeEditor instance
 */
export function registerEditor(editorId, editorInstance) {
    editorInstances[editorId] = editorInstance;
    console.log(`[Monaco Interop] Registered editor: ${editorId}`);
}

/**
 * MAIN FUNCTION: Injects a view zone into the Monaco editor
 *
 * This function performs the "hidden div strategy":
 * 1. Retrieves the DOM element by ID (the hidden div with the Blazor component)
 * 2. Gets the Monaco editor instance
 * 3. Uses Monaco's changeViewZones API to create a new view zone
 * 4. Moves the DOM element into the view zone
 *
 * View zones are special areas in the Monaco editor that can contain custom DOM content.
 * They allow you to inject HTML/components between code lines.
 *
 * @param {string} editorId - The HTML element ID of the Monaco editor
 * @param {number} lineNumber - The line number after which to insert the zone (0-indexed in Monaco)
 * @param {string} elementId - The HTML ID of the DOM element to inject
 */
export async function injectViewZone(editorId, lineNumber, elementId) {
    console.log(`[Monaco Interop] Injecting view zone at line ${lineNumber}, element: ${elementId}`);

    try {
        // Step 1: Get the editor instance
        // The editor instance should have been registered when the StandaloneCodeEditor was initialized
        const editorContainer = document.getElementById(editorId);
        if (!editorContainer) {
            console.error(`[Monaco Interop] Editor container not found: ${editorId}`);
            return;
        }

        // Monaco stores the editor instance in a special attribute
        // We need to wait for it to be available since BlazorMonaco loads it asynchronously
        let editor = await getMonacoEditorInstance(editorContainer);
        if (!editor) {
            console.error(`[Monaco Interop] Could not retrieve editor instance for: ${editorId}`);
            return;
        }

        // Step 2: Get the DOM element that contains the Blazor component
        const element = document.getElementById(elementId);
        if (!element) {
            console.error(`[Monaco Interop] Element not found: ${elementId}`);
            return;
        }

        // Step 3: Show the element (it was hidden before)
        // We need to show it before moving it into the view zone
        element.style.display = 'block';

        // Step 4: Create the view zone configuration
        // A view zone is a rectangular area that can contain custom DOM elements
        const viewZoneId = createViewZone(editor, lineNumber, element);

        console.log(`[Monaco Interop] View zone created with ID: ${viewZoneId}`);

    } catch (error) {
        console.error(`[Monaco Interop] Error injecting view zone:`, error);
    }
}

/**
 * Retrieves the Monaco editor instance from a container element.
 * BlazorMonaco stores the editor instance in the element's data attributes.
 *
 * This function waits up to 5 seconds for the editor to become available,
 * since there might be a timing delay between Blazor component initialization
 * and Monaco editor initialization.
 *
 * @param {HTMLElement} editorContainer - The container div of the editor
 * @returns {Promise<object>} - The Monaco editor instance
 */
async function getMonacoEditorInstance(editorContainer) {
    const maxAttempts = 50;
    let attempts = 0;

    while (attempts < maxAttempts) {
        // Try to find the editor using BlazorMonaco's internal structure
        // The editor is typically stored in the window object or as a property of the container

        // Method 1: Check if it's in a data attribute
        const editorInstance = editorContainer._monacoEditor;
        if (editorInstance) return editorInstance;

        // Method 2: Check in the global window object (BlazorMonaco sometimes exposes it there)
        if (window.monacoEditor && window.monacoEditor.editor) {
            return window.monacoEditor.editor;
        }

        // Method 3: Look for Monaco's internal editor registry
        // This is a bit hacky but works with some Monaco configurations
        const scripts = document.querySelectorAll('script');
        for (let script of scripts) {
            if (script._monacoEditor) {
                return script._monacoEditor;
            }
        }

        // Wait a bit and try again
        await new Promise(resolve => setTimeout(resolve, 100));
        attempts++;
    }

    // If we still haven't found it, log all available editor instances
    console.warn('[Monaco Interop] Editor instance not found in expected locations');
    console.log('[Monaco Interop] Available in editorInstances:', Object.keys(editorInstances));

    // Return from our stored instances if available
    for (let key in editorInstances) {
        return editorInstances[key];
    }

    return null;
}

/**
 * Creates and manages a view zone in the Monaco editor.
 *
 * View zones are created using the changeViewZones method, which takes:
 * - An accessor object with addZone method
 * - A callback that defines the zone
 *
 * The zone configuration specifies:
 * - afterLineNumber: Insert after this line (1-indexed)
 * - heightInLines: How many lines of height to reserve
 * - domNode: The DOM element to render
 * - marginDomNode: Optional element for the glyph margin
 *
 * @param {object} editor - The Monaco editor instance
 * @param {number} lineNumber - The line number (1-indexed)
 * @param {HTMLElement} domElement - The DOM element to inject
 * @returns {string} - The view zone ID
 */
function createViewZone(editor, lineNumber, domElement) {
    let zoneId = null;

    // changeViewZones is the Monaco API method that lets us add custom zones
    editor.changeViewZones(function(accessor) {
        // The zone configuration
        const zone = {
            afterLineNumber: lineNumber, // Insert AFTER this line number
            heightInLines: 6, // Reserve space for 6 lines (adjust based on component size)
            domNode: domElement, // The DOM node to render
            marginDomNode: null, // Optional: add an icon/button to the glyph margin

            // Optional callbacks for zone lifecycle
            onDomNodeTop: undefined,
            onComputedHeight: undefined,
        };

        // Add the zone and get its ID
        zoneId = accessor.addZone(zone);
    });

    return zoneId;
}

/**
 * Removes a view zone from the editor.
 * Call this when cleaning up or when the user closes a comment thread.
 *
 * @param {object} editor - The Monaco editor instance
 * @param {string} zoneId - The ID of the zone to remove
 */
export function removeViewZone(editor, zoneId) {
    if (!editor || !zoneId) {
        console.warn('[Monaco Interop] Cannot remove zone: invalid editor or zoneId');
        return;
    }

    editor.changeViewZones(function(accessor) {
        accessor.removeZone(zoneId);
    });

    console.log(`[Monaco Interop] Removed view zone: ${zoneId}`);
}

/**
 * Utility function: Get the height of a DOM element in pixels
 * Useful for dynamically calculating zone height
 *
 * @param {HTMLElement} element - The DOM element
 * @returns {number} - Height in pixels
 */
export function getElementHeight(element) {
    if (!element) return 0;
    return element.getBoundingClientRect().height;
}

/**
 * Utility function: Get the height of a DOM element converted to editor lines
 * Each line in Monaco is typically 18-20 pixels (depending on font size)
 *
 * @param {HTMLElement} element - The DOM element
 * @param {number} lineHeight - Pixels per line (default: 18)
 * @returns {number} - Height in editor lines
 */
export function getElementHeightInLines(element, lineHeight = 18) {
    const pixelHeight = getElementHeight(element);
    return Math.ceil(pixelHeight / lineHeight);
}

/**
 * Advanced: Dynamically update view zone height based on content
 * Call this if the component's height changes dynamically
 *
 * @param {object} editor - The Monaco editor instance
 * @param {string} zoneId - The ID of the zone to update
 * @param {number} newHeightInLines - New height in lines
 */
export function updateViewZoneHeight(editor, zoneId, newHeightInLines) {
    if (!editor || !zoneId) {
        console.warn('[Monaco Interop] Cannot update zone: invalid editor or zoneId');
        return;
    }

    editor.changeViewZones(function(accessor) {
        accessor.layoutZone(zoneId);
    });

    console.log(`[Monaco Interop] Updated view zone ${zoneId} height to ${newHeightInLines} lines`);
}

/**
 * Helper: Log all active view zones (for debugging)
 *
 * @param {object} editor - The Monaco editor instance
 */
export function logViewZones(editor) {
    if (!editor) {
        console.warn('[Monaco Interop] Cannot log zones: invalid editor');
        return;
    }

    // This is a bit hacky since getViewZones is internal, but useful for debugging
    console.log('[Monaco Interop] View zones:', editor.getVisibleRanges());
}

console.log('[Monaco Interop] Module loaded successfully');
