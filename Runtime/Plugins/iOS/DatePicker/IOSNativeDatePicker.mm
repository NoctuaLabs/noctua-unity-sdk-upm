#import "IOSNativeDatePicker.h"

@implementation IOSNativeDatePicker

+ (CGFloat)GetW {
    UIViewController *vc = UnityGetGLViewController();
    UIInterfaceOrientation orientation = [UIApplication sharedApplication].statusBarOrientation;
    BOOL isLandscape = UIInterfaceOrientationIsLandscape(orientation);
    CGFloat width = isLandscape ? vc.view.frame.size.height : vc.view.frame.size.width;

    // Adjust for iOS 8+ where the frame size does not rotate with orientation
    if (@available(iOS 8.0, *)) {
        width = vc.view.frame.size.width;
    }

    return width;
}

+ (void)DP_changeDate:(UIDatePicker *)sender {
    NSDateFormatter *dateFormatter = [[NSDateFormatter alloc] init];
    [dateFormatter setDateFormat:@"yyyy-MM-dd HH:mm:ss"];
    NSString *dateString = [dateFormatter stringFromDate:sender.date];
    int pickerId = (int)sender.tag;

    NSString *unityObjectName = [NSString stringWithFormat:@"MobileDateTimePicker_%d", pickerId];
    UnitySendMessage([unityObjectName UTF8String], "DateChangedEvent", [dateString UTF8String]);
}

+ (void)DP_doneButtonClicked:(UIBarButtonItem *)sender {
    [self DP_dismissDatePicker:nil];
}

+ (void)DP_PickerClosed:(UIDatePicker *)sender {
    NSDateFormatter *dateFormatter = [[NSDateFormatter alloc] init];
    [dateFormatter setDateFormat:@"yyyy-MM-dd HH:mm:ss"];
    NSString *dateString = [dateFormatter stringFromDate:sender.date];
    int pickerId = (int)sender.tag;

    NSString *unityObjectName = [NSString stringWithFormat:@"MobileDateTimePicker_%d", pickerId];
    UnitySendMessage([unityObjectName UTF8String], "PickerClosedEvent", [dateString UTF8String]);
}

UIDatePicker *datePicker;

+ (void)DP_show:(int)mode secondNumber:(double)unix pickerId:(int)pickerId {
    UIViewController *vc = UnityGetGLViewController();

    CGRect toolbarTargetFrame = CGRectMake(0, vc.view.bounds.size.height - 260, [self GetW], 44);
    CGRect datePickerTargetFrame = CGRectMake(0, vc.view.bounds.size.height - 216, [self GetW], 216);
    CGRect darkViewTargetFrame = CGRectMake(0, vc.view.bounds.size.height - 260, [self GetW], 260);

    // Create the darkView with a solid background
    UIView *darkView = [[UIView alloc] initWithFrame:CGRectMake(0, vc.view.bounds.size.height, [self GetW], 260)];
    darkView.alpha = 1; // Fully opaque
    if (@available(iOS 13.0, *)) {
        darkView.backgroundColor = [UIColor systemGray5Color];
    } else {
        if (@available(iOS 12.0, *)) {
            if (darkView.traitCollection.userInterfaceStyle == UIUserInterfaceStyleDark) {
                darkView.backgroundColor = [UIColor blackColor];
            } else {
                darkView.backgroundColor = [UIColor whiteColor];
            }
        } else {
            darkView.backgroundColor = [UIColor whiteColor];
        }
    }
    darkView.tag = 9;

    // Add a tap gesture to dismiss the date picker
    UITapGestureRecognizer *tapGesture = [[UITapGestureRecognizer alloc] initWithTarget:self action:@selector(DP_dismissDatePicker:)];
    [darkView addGestureRecognizer:tapGesture];
    [vc.view addSubview:darkView];

    datePicker = [[UIDatePicker alloc] initWithFrame:CGRectMake(0, vc.view.bounds.size.height, [self GetW], 216)];
    datePicker.tag = pickerId;

    [datePicker addTarget:self action:@selector(DP_changeDate:) forControlEvents:UIControlEventValueChanged];

    switch (mode) {
        case 1:
            datePicker.datePickerMode = UIDatePickerModeTime;
            break;
        case 2:
            datePicker.datePickerMode = UIDatePickerModeDate;
            if (@available(iOS 13.4, *)) {
                datePicker.preferredDatePickerStyle = UIDatePickerStyleWheels;
            }
            NSDate *date = [NSDate dateWithTimeIntervalSince1970:unix];
            [datePicker setDate:date];
            break;
    }
    [vc.view addSubview:datePicker];

    UIToolbar *toolbar = [[UIToolbar alloc] initWithFrame:CGRectMake(0, vc.view.bounds.size.height, [self GetW], 44)];
    toolbar.tag = 11;
    toolbar.barStyle = UIBarStyleDefault;
    UIBarButtonItem *spacer = [[UIBarButtonItem alloc] initWithBarButtonSystemItem:UIBarButtonSystemItemFlexibleSpace target:nil action:nil];
    UIBarButtonItem *doneButton = [[UIBarButtonItem alloc] initWithBarButtonSystemItem:UIBarButtonSystemItemDone target:self action:@selector(DP_dismissDatePicker:)];

    [toolbar setItems:@[spacer, doneButton]];
    [vc.view addSubview:toolbar];

    [UIView animateWithDuration:0.3 animations:^{
        toolbar.frame = toolbarTargetFrame;
        datePicker.frame = datePickerTargetFrame;
        darkView.frame = darkViewTargetFrame;
    }];
}



+ (void)DP_removeViews {
    UIViewController *vc = UnityGetGLViewController();
    [[vc.view viewWithTag:9] removeFromSuperview];  // Dark view
    [[vc.view viewWithTag:11] removeFromSuperview]; // Toolbar
    [datePicker removeFromSuperview];              // Date picker
    datePicker = nil;
}

+ (void)DP_dismissDatePicker:(id)sender {
    UIViewController *vc = UnityGetGLViewController();

    // Check if the date picker is currently shown
    if (!datePicker || !datePicker.superview || datePicker.frame.origin.y >= vc.view.bounds.size.height) {
        NSLog(@"DatePicker is not currently shown.");
        return; // Exit if the date picker is not visible
    }

    [self DP_PickerClosed:datePicker];

    CGRect offscreenFrame = CGRectMake(0, vc.view.bounds.size.height, [self GetW], 260);

    [UIView animateWithDuration:0.3 animations:^{
        datePicker.frame = offscreenFrame;
        // Assuming other views are directly referenced:
        if (datePicker.superview) {
            for (UIView *subview in datePicker.superview.subviews) {
                if (subview != datePicker) {
                    subview.frame = offscreenFrame;
                }
            }
        }
    } completion:^(BOOL finished) {
        [self DP_removeViews];
    }];
}

extern "C" {
    
    //--------------------------------------
    //  Unity Call Date Time Picker
    //--------------------------------------

    void _TAG_ShowDatePicker(int mode, double unix, int pickerId) {
        [IOSNativeDatePicker DP_show:mode secondNumber:unix pickerId:pickerId];
    }
    
    void DismissDatePicker() {
        [IOSNativeDatePicker DP_dismissDatePicker:nil];
    }
}

@end
