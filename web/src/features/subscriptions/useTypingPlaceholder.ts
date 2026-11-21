import { useEffect, useState } from "react";

const START_DELAY_MS = 700;
const TYPE_MS = 55;
const HOLD_MS = 1800;
const DELETE_MS = 25;
const NEXT_MS = 500;

function prefersReducedMotion() {
  return typeof window.matchMedia === "function" && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
}

// The text typed out so far: each of `texts` is typed, held, deleted, then the next one starts, looping.
// While `paused` nothing advances. With reduced motion it is simply the first text, whole.
// `texts` should be a stable array (e.g. a module constant), since a new one restarts the animation.
export function useTypingPlaceholder(texts: string[], paused: boolean): string {
  const [typed, setTyped] = useState("");
  const reduceMotion = prefersReducedMotion();

  useEffect(() => {
    if (paused || reduceMotion || texts.length === 0) {
      return;
    }

    let index = 0;
    let length = 0;
    let deleting = false;
    let timer: ReturnType<typeof setTimeout>;

    function tick() {
      const text = texts[index % texts.length];
      if (!deleting) {
        length++;
        setTyped(text.slice(0, length));
        if (length === text.length) {
          deleting = true;
          timer = setTimeout(tick, HOLD_MS);
        } else {
          timer = setTimeout(tick, TYPE_MS);
        }
      } else {
        length--;
        setTyped(text.slice(0, length));
        if (length === 0) {
          deleting = false;
          index++;
          timer = setTimeout(tick, NEXT_MS);
        } else {
          timer = setTimeout(tick, DELETE_MS);
        }
      }
    }

    timer = setTimeout(tick, START_DELAY_MS);
    return () => clearTimeout(timer);
  }, [texts, paused, reduceMotion]);

  return reduceMotion ? (texts[0] ?? "") : typed;
}
