import { Directive, forwardRef } from "@angular/core";
import { AbstractControl, NG_VALIDATORS, ValidationErrors, Validator } from "@angular/forms";

@Directive({
  selector: "[appPasswordMatch]",
  standalone: true,
  providers: [{ provide: NG_VALIDATORS, useExisting: forwardRef(() => PasswordMatchDirective), multi: true }],
})
export class PasswordMatchDirective implements Validator {
  validate(control: AbstractControl): ValidationErrors | null {
    const password = control.get("password");
    const confirmation = control.get("confirmPassword");
    // Edit mode has no password controls; password reset remains a separate action.
    if (!password || !confirmation) return null;
    // Required validators handle empty fields; compare only entered values.
    if (!password.value || !confirmation.value) return null;
    return password.value === confirmation.value ? null : { passwordMismatch: true };
  }
}
