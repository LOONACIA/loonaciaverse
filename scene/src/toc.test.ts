import { afterEach, describe, expect, it, vi } from "vitest";
import { scrollToHeading } from "./toc";

afterEach(() => {
  document.body.innerHTML = "";
  vi.restoreAllMocks();
});

describe("scrollToHeading", () => {
  it.each([false, true])("scrolls to a Korean heading with reduced motion set to %s", (reducedMotion) => {
    const heading = document.createElement("h2");
    heading.id = "나만의-우주를-시작하며";
    heading.scrollIntoView = vi.fn();
    document.body.append(heading);
    vi.spyOn(window, "matchMedia").mockReturnValue({ matches: reducedMotion } as MediaQueryList);
    const originalUrl = window.location.href;
    const pushState = vi.spyOn(window.history, "pushState");
    const replaceState = vi.spyOn(window.history, "replaceState");

    scrollToHeading(heading.id);

    expect(window.matchMedia).toHaveBeenCalledWith("(prefers-reduced-motion: reduce)");
    expect(heading.scrollIntoView).toHaveBeenCalledExactlyOnceWith({ block: "start", behavior: reducedMotion ? "instant" : "smooth" });
    expect(window.location.href).toBe(originalUrl);
    expect(pushState).not.toHaveBeenCalled();
    expect(replaceState).not.toHaveBeenCalled();
  });

  it("ignores a missing heading without throwing", () => {
    const matchMedia = vi.spyOn(window, "matchMedia");

    expect(() => scrollToHeading("없는-제목")).not.toThrow();
    expect(matchMedia).not.toHaveBeenCalled();
  });
});
