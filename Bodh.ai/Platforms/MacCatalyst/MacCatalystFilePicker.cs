using Foundation;
using Microsoft.Maui.Storage;
using UIKit;

namespace Bodh.ai;

internal static class MacCatalystFilePicker
{
    public static Task<FileResult?> PickAsync(string[] allowedContentTypes)
    {
        var completionSource = new TaskCompletionSource<FileResult?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                var picker = new UIDocumentPickerViewController(
                    allowedContentTypes.Length == 0 ? new[] { "public.item" } : allowedContentTypes,
                    UIDocumentPickerMode.Import);

                var pickerDelegate = new DocumentPickerDelegate(completionSource);
                picker.Delegate = pickerDelegate;
                picker.ModalPresentationStyle = UIModalPresentationStyle.FormSheet;

                var presenter = GetPresenter();
                if (presenter is null)
                {
                    completionSource.TrySetException(
                        new InvalidOperationException("Unable to locate the active MacCatalyst window."));
                    return;
                }

                presenter.PresentViewController(picker, true, null);
            }
            catch (Exception exception)
            {
                completionSource.TrySetException(exception);
            }
        });

        return completionSource.Task;
    }

    private static UIViewController? GetPresenter()
    {
        var windowScene = UIApplication.SharedApplication.ConnectedScenes
            .OfType<UIWindowScene>()
            .FirstOrDefault(scene => scene.ActivationState == UISceneActivationState.ForegroundActive);

        var window = windowScene?.Windows.FirstOrDefault(candidate => candidate.IsKeyWindow)
            ?? windowScene?.Windows.FirstOrDefault();

        var presenter = window?.RootViewController;
        while (presenter?.PresentedViewController is not null)
        {
            presenter = presenter.PresentedViewController;
        }

        return presenter;
    }

    private sealed class DocumentPickerDelegate : UIDocumentPickerDelegate
    {
        private readonly TaskCompletionSource<FileResult?> _completionSource;

        public DocumentPickerDelegate(TaskCompletionSource<FileResult?> completionSource)
        {
            _completionSource = completionSource;
        }

        public override void DidPickDocument(UIDocumentPickerViewController controller, NSUrl[] urls)
        {
            var selectedUrl = urls.FirstOrDefault();
            _completionSource.TrySetResult(
                selectedUrl is null ? null : new FileResult(selectedUrl.Path));
            controller.DismissViewController(true, null);
        }

        public override void WasCancelled(UIDocumentPickerViewController controller)
        {
            _completionSource.TrySetResult(null);
            controller.DismissViewController(true, null);
        }
    }
}
