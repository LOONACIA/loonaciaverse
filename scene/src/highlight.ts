import hljs from "highlight.js/lib/core";
import csharp from "highlight.js/lib/languages/csharp";
import cpp from "highlight.js/lib/languages/cpp";
import typescript from "highlight.js/lib/languages/typescript";
import javascript from "highlight.js/lib/languages/javascript";
import json from "highlight.js/lib/languages/json";
import xml from "highlight.js/lib/languages/xml";
import css from "highlight.js/lib/languages/css";
import bash from "highlight.js/lib/languages/bash";
import powershell from "highlight.js/lib/languages/powershell";
import yaml from "highlight.js/lib/languages/yaml";
import glsl from "highlight.js/lib/languages/glsl";

for (const [name, language] of Object.entries({ csharp, cpp, typescript, javascript, json, xml, css, bash, powershell, yaml, glsl })) {
  hljs.registerLanguage(name, language);
}

export function highlightAll(root: HTMLElement): void {
  for (const code of root.querySelectorAll<HTMLElement>('pre code[class*="language-"]:not([data-highlighted])')) {
    const language = /\blanguage-([\w-]+)/i.exec(code.className)?.[1];
    if (language && hljs.getLanguage(language)) {
      hljs.highlightElement(code);
    }
  }
}
