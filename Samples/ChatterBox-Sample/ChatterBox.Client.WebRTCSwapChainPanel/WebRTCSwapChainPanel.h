//*********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************

#pragma once

namespace ChatterBox {
namespace Client {
namespace WebRTCSwapChainPanel {

	[Windows::Foundation::Metadata::WebHostHidden] // to fix C4453 warning
    public ref class WebRTCSwapChainPanel sealed : Windows::UI::Xaml::Controls::SwapChainPanel
    {
    public:
        WebRTCSwapChainPanel();
        virtual ~WebRTCSwapChainPanel();
        property int64 SwapChainPanelHandle
        {
            void set(int64);
            int64 get();
        }
        property Windows::Foundation::Size NativeVideoSize
        {
            void set(Windows::Foundation::Size);
            Windows::Foundation::Size get();
        }
        static property uint32 CurrentProcessId
        {
            uint32 get();
        }
		
		static property Windows::UI::Xaml::DependencyProperty^ SwapChainPanelHandleProperty
		{
			Windows::UI::Xaml::DependencyProperty^ get()
			{
				return _swapChainPanelHandleProperty;
			}
		}
		static property Windows::UI::Xaml::DependencyProperty^ SizeProperty
		{
			Windows::UI::Xaml::DependencyProperty^ get()
			{
				return _intSizeProperty;
			}
		}

    private:
        void UpdateHandle(int64 handle);

        static void OnSwapChainPanelHandleChanged(Windows::UI::Xaml::DependencyObject^ d,
            Windows::UI::Xaml::DependencyPropertyChangedEventArgs^ e);
        static void OnNativeVideoSizeChanged(Windows::UI::Xaml::DependencyObject^ d,
            Windows::UI::Xaml::DependencyPropertyChangedEventArgs^ e);

        HANDLE _handle;
        Windows::Foundation::Size _nativeVideoSize;
        Windows::Foundation::Size _controlSize;        

        static Windows::UI::Xaml::DependencyProperty^  _swapChainPanelHandleProperty;
        static Windows::UI::Xaml::DependencyProperty^  _intSizeProperty;

        void OnSizeChanged(Platform::Object ^sender, Windows::UI::Xaml::SizeChangedEventArgs ^e);

    };

}}}
