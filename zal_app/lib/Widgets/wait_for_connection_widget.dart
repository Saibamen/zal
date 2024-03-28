import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:zal/Functions/models.dart';
import 'package:zal/Functions/utils.dart';
import 'package:zal/Screens/HomeScreen/Providers/home_screen_providers.dart';
import 'package:zal/Screens/HomeScreen/Providers/is_computer_online_on_server_provider.dart';
import 'package:zal/Screens/HomeScreen/Providers/webrtc_provider.dart';
import 'package:zal/Screens/HomeScreen/home_screen.dart';
import 'package:zal/Screens/MainScreen/main_screen_providers.dart';

class WaitForConnectionWidget extends ConsumerWidget {
  const WaitForConnectionWidget({super.key, required this.child});
  final Widget child;
  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final connectionEstablishment = ref.watch(connectionEstablishmentProvider);
    if (connectionEstablishment.shouldShowConnectedWidget) {
      return child;
    }
    return Center(
      child: ListView(
        shrinkWrap: true,
        children: [
          Center(
            child: Table(
              columnWidths: const {
                0: IntrinsicColumnWidth(),
                1: IntrinsicColumnWidth(),
              },
              children: [
                tableRow(context, '', connectionEstablishment.isConnectedToServer ? Icons.check : Icons.close, "Connecting to server"),
                tableRow(context, '', connectionEstablishment.isComputerOnlineOnServer ? Icons.check : Icons.close, "waiting for PC to come online"),
                tableRow(context, '', connectionEstablishment.isWebrtcConnected ? Icons.check : Icons.close, "establishing peer-to-peer connection"),
                tableRow(context, '', connectionEstablishment.hasReceivedData ? Icons.check : Icons.close, "waiting for PC to send first data"),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

final connectionEstablishmentProvider = StateProvider<ConnectionEstablishment>((ref) {
  final isConnectedToServer = ref.watch(isConnectedToServerProvider);
  final isComputerOnlineOnServer = ref.watch(isComputerOnlineOnServerProvider);
  final isWebrtcConnected = ref.watch(webrtcProvider.select((value) => value.isConnected));
  WebrtcData? webrtcData;
  final shouldListenToWebrtcDataChanges = ref.read(shouldListenToWebrtcDataChangesProvider);
  if (isWebrtcConnected == false) {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      ref.read(shouldListenToWebrtcDataChangesProvider.notifier).state = true;
    });
  }
  if (shouldListenToWebrtcDataChanges) {
    webrtcData = ref.watch(webrtcProvider.select((value) => value.data));
    if (webrtcData != null) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        ref.read(shouldListenToWebrtcDataChangesProvider.notifier).state = false;
      });
    }
  }
  bool shouldShowConnectedWidget = isConnectedToServer &&
      isComputerOnlineOnServer.valueOrNull == true &&
      isWebrtcConnected &&
      (webrtcData != null || shouldListenToWebrtcDataChanges == false);

  return ConnectionEstablishment(
      isConnectedToServer: isConnectedToServer,
      isComputerOnlineOnServer: isComputerOnlineOnServer.valueOrNull ?? false,
      isWebrtcConnected: isWebrtcConnected,
      hasReceivedData: (webrtcData != null || shouldListenToWebrtcDataChanges == false),
      shouldShowConnectedWidget: shouldShowConnectedWidget);
});

//this provider checks the connection establishment process, and will try to resolve issues if connection is stuck
final connectionEstablishmentFixerProvider = FutureProvider((ref) async {
  final connectionEstablishment = ref.watch(connectionEstablishmentProvider);

  await Future.delayed(const Duration(seconds: 5));
  final newConnectionEstablishment = ref.read(connectionEstablishmentProvider);
  final timer = Timer(const Duration(seconds: 5), () {
    if (connectionEstablishment == newConnectionEstablishment) {
      if (connectionEstablishment.shouldShowConnectedWidget == false) {
        //stuck connection detected, do what you can to fix it
        if (connectionEstablishment.isConnectedToServer == false || connectionEstablishment.isComputerOnlineOnServer == false) {
          ref.invalidate(socketObjectProvider);
          showSnackbar("stuck connection detected, reconnecting to server...", ref.read(contextProvider)!);
        } else if (connectionEstablishment.isWebrtcConnected == false) {
          ref.read(webrtcProvider.notifier).initiateConnection();
          showSnackbar("stuck connection detected, re-establishing connection to PC...", ref.read(contextProvider)!);
        }
      }
    }
    ref.invalidateSelf();
  });
  ref.onDispose(timer.cancel);
});
