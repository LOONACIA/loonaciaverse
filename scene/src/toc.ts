export function scrollToHeading(id: string): void {
  const heading = document.getElementById(id);
  if (!heading) {
    return;
  }

  const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  heading.scrollIntoView({ block: "start", behavior: reducedMotion ? "instant" : "smooth" });
}
