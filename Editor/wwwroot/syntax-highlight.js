/**
 * SYNTAX HIGHLIGHTING JAVASCRIPT INTEROP
 *
 * This module provides syntax highlighting functionality for code snippets
 * embedded in comments using the Highlight.js library.
 *
 * Highlight.js is a lightweight, production-ready syntax highlighter
 * that supports 180+ languages and 80+ themes.
 *
 * Usage:
 * - Call highlightCode(elementId, language) to highlight a code block
 * - Call highlightAllCode() to auto-detect and highlight all <pre><code> blocks
 */

/**
 * Highlights a specific code block by element ID
 *
 * @param {string} elementId - The ID of the element containing the code
 * @param {string} language - The programming language (e.g., 'csharp', 'javascript', 'python')
 */
export function highlightCode(elementId, language) {
    const element = document.getElementById(elementId);
    if (!element) {
        console.warn(`[Syntax Highlight] Element not found: ${elementId}`);
        return;
    }

    try {
        // Set the language class if provided
        if (language) {
            element.className = `language-${language}`;
        }

        // Highlight the element using Highlight.js
        hljs.highlightElement(element);
        console.log(`[Syntax Highlight] Highlighted element: ${elementId} as ${language}`);
    } catch (error) {
        console.error(`[Syntax Highlight] Error highlighting code:`, error);
    }
}

/**
 * Auto-detects and highlights all code blocks on the page
 * Useful if you're rendering multiple code snippets dynamically
 */
export function highlightAllCode() {
    try {
        document.querySelectorAll('pre code').forEach((block) => {
            hljs.highlightElement(block);
        });
        console.log('[Syntax Highlight] Highlighted all code blocks');
    } catch (error) {
        console.error(`[Syntax Highlight] Error highlighting all blocks:`, error);
    }
}

/**
 * Gets the list of supported languages in Highlight.js
 * Useful for language selection dropdowns
 *
 * @returns {Array<string>} - Array of supported language names
 */
export function getSupportedLanguages() {
    if (!hljs || !hljs.listLanguages) {
        return [];
    }
    return hljs.listLanguages();
}

/**
 * Advanced: Create a highlighted code element without needing an HTML element
 * Returns the highlighted HTML as a string
 *
 * @param {string} code - The source code to highlight
 * @param {string} language - The programming language
 * @returns {string} - The highlighted HTML
 */
export function highlightCodeString(code, language) {
    try {
        if (!hljs) {
            console.warn('[Syntax Highlight] Highlight.js not loaded');
            return code;
        }

        if (language) {
            const result = hljs.highlight(code, { language });
            return result.value;
        } else {
            const result = hljs.highlightAuto(code);
            return result.value;
        }
    } catch (error) {
        console.error(`[Syntax Highlight] Error highlighting string:`, error);
        return code;
    }
}

console.log('[Syntax Highlight] Module loaded. Highlight.js version:', hljs?.version || 'unknown');
