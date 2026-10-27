import type { ButtonHTMLAttributes } from 'react';
import { Link, type LinkProps } from 'react-router';
import { cx } from './cx';

type Variant = 'primary' | 'secondary' | 'ghost';

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant;
}

export function Button({ variant = 'primary', className, type = 'button', ...rest }: ButtonProps) {
  return <button type={type} className={cx('btn', `btn--${variant}`, className)} {...rest} />;
}

export interface LinkButtonProps extends LinkProps {
  variant?: Variant;
}

export function LinkButton({ variant = 'secondary', className, ...rest }: LinkButtonProps) {
  return <Link className={cx('btn', `btn--${variant}`, className)} {...rest} />;
}
