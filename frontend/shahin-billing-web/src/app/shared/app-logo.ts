import { Component, input } from '@angular/core';

/**
 * The Shahin Enterprises logo as inline SVG. Its colours come from the theme (--logo-ink, --logo-warm, --logo-pillow),
 * so it changes together with the colour theme. Draw it on a light background.
 * variant "icon" is only the sleeping scene (the "I" of SHAHIN), for narrow spaces.
 */
@Component({
  selector: 'app-logo',
  template: `
    <!-- width/height attributes: a small size to start from, so the logo is never drawn huge before the styles load -->
    <svg xmlns="http://www.w3.org/2000/svg" [attr.viewBox]="variant() === 'icon' ? '384 54 88 76' : '104 40 468 136'"
         [attr.width]="variant() === 'icon' ? 46 : 220" [attr.height]="variant() === 'icon' ? 40 : 64"
         role="img" aria-label="Shahin Enterprises" class="logo">
      @if (variant() === 'full') {
        <text class="mo ink" x="382" y="120" text-anchor="end" font-size="80" font-weight="800" letter-spacing="4">SHAH</text>
        <text class="mo ink" x="474" y="120" font-size="80" font-weight="800">N</text>
        <text class="mo ink" x="340" y="158" text-anchor="middle" font-size="13" font-weight="500" letter-spacing="8">ENTERPRISES</text>
      }
      <rect class="ink" x="388" y="104" width="80" height="16" rx="3" />
      <path class="pillow" d="M392,90 Q404,85 416,90 Q419,96 416,102 Q404,106 392,102 Q389,96 392,90 Z" />
      <path class="warm" d="M418,102 Q443,94 468,101 Q474,112 470,124 Q444,121 418,122 Z" />
      <text class="mo warm" x="422" y="82" font-size="14" font-weight="800">z</text>
      <text class="mo warm" x="436" y="68" font-size="18" font-weight="800">z</text>
    </svg>
  `,
  styles: `
    :host { display: block; max-width: 100%; }
    .logo { display: block; width: 100%; height: auto; }
    .mo { font-family: Montserrat, 'Manrope', sans-serif; }
    .ink { fill: var(--logo-ink, #1e2a5a); transition: fill 0.35s ease; }
    .warm { fill: var(--logo-warm, #e0a43f); transition: fill 0.35s ease; }
    .pillow { fill: var(--logo-pillow, #f6d8c4); stroke: var(--logo-ink, #1e2a5a); stroke-width: 1.5; transition: fill 0.35s ease, stroke 0.35s ease; }
  `,
})
export class AppLogo {
  variant = input<'full' | 'icon'>('full');
}
