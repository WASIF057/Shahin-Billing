import { Pipe, PipeTransform } from '@angular/core';
import { formatIndian } from '../core/gst';

/** {{ 123456 | inr }} -> ₹1,23,456.00   ({{ x | inr:false }} drops the ₹ sign) */
@Pipe({ name: 'inr' })
export class InrPipe implements PipeTransform {
  transform(value: number | null | undefined, symbol = true, decimals = 2): string {
    return (symbol ? '₹' : '') + formatIndian(value ?? 0, decimals);
  }
}
