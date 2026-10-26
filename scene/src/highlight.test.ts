import { afterEach, describe, expect, it } from "vitest";
import { highlightAll } from "./highlight";

afterEach(() => {
  document.body.innerHTML = "";
});

function createRoot(html: string): HTMLDivElement {
  const root = document.createElement("div");
  root.innerHTML = html;
  document.body.append(root);
  return root;
}

describe("highlightAll", () => {
  it("highlights C# keywords", () => {
    const root = createRoot('<pre><code class="language-csharp">public class Example { }</code></pre>');

    highlightAll(root);

    expect(root.querySelector(".hljs-keyword")?.textContent).toBe("public");
    expect(root.querySelector("code")?.dataset.highlighted).toBe("yes");
  });

  it("leaves unregistered languages and blocks without a language unchanged", () => {
    const root = createRoot('<pre><code class="language-unknown">public class Example { }</code></pre><pre><code>public class Plain { }</code></pre>');
    const original = root.innerHTML;

    highlightAll(root);

    expect(root.innerHTML).toBe(original);
  });

  it("is idempotent", () => {
    const root = createRoot('<pre><code class="language-csharp">public class Example { }</code></pre>');
    highlightAll(root);
    const highlighted = root.innerHTML;

    highlightAll(root);

    expect(root.innerHTML).toBe(highlighted);
  });

  it("leaves blocks outside the root unchanged", () => {
    const outside = createRoot('<pre><code class="language-csharp">public class Outside { }</code></pre>');
    const original = outside.innerHTML;
    const root = createRoot('<pre><code class="language-csharp">public class Inside { }</code></pre>');

    highlightAll(root);

    expect(outside.innerHTML).toBe(original);
    expect(root.querySelector(".hljs-keyword")).not.toBeNull();
  });

  it("leaves previously highlighted blocks unchanged", () => {
    const root = createRoot('<pre><code class="language-csharp" data-highlighted="">public class Example { }</code></pre>');
    const original = root.innerHTML;

    highlightAll(root);

    expect(root.innerHTML).toBe(original);
  });

  it("supports registered aliases", () => {
    const root = createRoot('<pre><code class="language-cs">public class Example { }</code></pre>');

    highlightAll(root);

    expect(root.querySelector(".hljs-keyword")?.textContent).toBe("public");
  });
});
