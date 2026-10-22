#ifndef SC_FADE_INCLUDED
#define SC_FADE_INCLUDED
// F6 round 4: a 4x4 ordered-dither threshold for the screen-door fade, so a swap dissolves instead of popping. Only a fade
// variant or a fade shader may call it: a discard anywhere in a shader costs Apple GPUs their hidden-surface removal for
// every draw of it. Kit.shader's _SC_FADE (the kit-to-model swap) and CollectionFade.shader (the
// Collection's models) share it.
static const float ScBayer[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

// Discards the share 1 - visible of the pixels, in a fixed screen pattern.
void ScFadeClip(float2 positionCS, half visible)
{
    int2 bp = int2(positionCS) & 3;
    clip(visible - (ScBayer[bp.y * 4 + bp.x] + 0.5) / 16.0);
}
#endif
