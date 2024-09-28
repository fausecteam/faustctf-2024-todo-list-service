// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.
// script.js
// Function to open a specific modal
function openModal(modalId) {
    const modal = document.getElementById(modalId);
    if (modal) {
        modal.style.display = 'flex';
    }
}

// Function to close a specific modal
function closeModal(modalId) {
    const modal = document.getElementById(modalId);
    if (modal) {
        modal.style.display = 'none';
    }
}

// Attach event handlers when the DOM is fully loaded
document.addEventListener('DOMContentLoaded', function () {
    // Handle open modal buttons
    document.querySelectorAll('[data-modal-id]').forEach(button => {
        button.onclick = function () {
            openModal(this.getAttribute('data-modal-id'));
        }
    });

    document.querySelectorAll('.close-btn').forEach(button => {
        button.onclick = function () {
            closeModal(this.closest('.modal').id);
        }
    });

    // Handle click outside modal to close it
    window.onclick = function (event) {
        if (event.target.classList.contains('modal')) {
            closeModal(event.target.id);
        }
    };
});