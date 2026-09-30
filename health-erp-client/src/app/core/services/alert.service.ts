import { Injectable } from '@angular/core';
import Swal from 'sweetalert2';

@Injectable({
  providedIn: 'root'
})
export class AlertService {
  /**
   * Displays a SweetAlert2 success popup after every save/update/action.
   */
  success(title: string = 'Successful!', text: string = 'Saved successfully.'): void {
    Swal.fire({
      title: title,
      text: text,
      icon: 'success',
      confirmButtonText: 'OK',
      confirmButtonColor: '#10b981',
      timer: 3000,
      timerProgressBar: true
    });
  }

  /**
   * Displays a SweetAlert2 error popup.
   */
  error(title: string = 'Error', text: string = 'An error occurred while saving.'): void {
    Swal.fire({
      title: title,
      text: text,
      icon: 'error',
      confirmButtonText: 'OK',
      confirmButtonColor: '#ef4444'
    });
  }

  /**
   * Displays a SweetAlert2 warning popup.
   */
  warning(title: string = 'Warning', text: string): void {
    Swal.fire({
      title: title,
      text: text,
      icon: 'warning',
      confirmButtonText: 'OK',
      confirmButtonColor: '#f59e0b'
    });
  }

  /**
   * Displays a confirmation dialog before performing critical actions.
   */
  confirm(title: string, text: string, confirmText: string = 'Yes, proceed'): Promise<boolean> {
    return Swal.fire({
      title: title,
      text: text,
      icon: 'question',
      showCancelButton: true,
      confirmButtonColor: '#3b82f6',
      cancelButtonColor: '#6b7280',
      confirmButtonText: confirmText
    }).then((result) => result.isConfirmed);
  }
}
