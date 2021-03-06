﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace CayugaConnector.ServiceReference1 {
    
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ServiceModel.ServiceContractAttribute(ConfigurationName="ServiceReference1.IReceiverServiceResponse")]
    public interface IReceiverServiceResponse {
        
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/IReceiverServiceResponse/SendAlarm", ReplyAction="http://tempuri.org/IReceiverServiceResponse/SendAlarmResponse")]
        string SendAlarm(string requestData);
        
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/IReceiverServiceResponse/SendAlarm", ReplyAction="http://tempuri.org/IReceiverServiceResponse/SendAlarmResponse")]
        System.Threading.Tasks.Task<string> SendAlarmAsync(string requestData);
        
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/IReceiverServiceResponse/SendHeartBeat", ReplyAction="http://tempuri.org/IReceiverServiceResponse/SendHeartBeatResponse")]
        string SendHeartBeat(string requestData);
        
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/IReceiverServiceResponse/SendHeartBeat", ReplyAction="http://tempuri.org/IReceiverServiceResponse/SendHeartBeatResponse")]
        System.Threading.Tasks.Task<string> SendHeartBeatAsync(string requestData);
        
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/IReceiverServiceResponse/GetNTPServerInfo", ReplyAction="http://tempuri.org/IReceiverServiceResponse/GetNTPServerInfoResponse")]
        string GetNTPServerInfo(string requestData);
        
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/IReceiverServiceResponse/GetNTPServerInfo", ReplyAction="http://tempuri.org/IReceiverServiceResponse/GetNTPServerInfoResponse")]
        System.Threading.Tasks.Task<string> GetNTPServerInfoAsync(string requestData);
        
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/IReceiverServiceResponse/SendRecorderInfo", ReplyAction="http://tempuri.org/IReceiverServiceResponse/SendRecorderInfoResponse")]
        string SendRecorderInfo(string requestData);
        
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/IReceiverServiceResponse/SendRecorderInfo", ReplyAction="http://tempuri.org/IReceiverServiceResponse/SendRecorderInfoResponse")]
        System.Threading.Tasks.Task<string> SendRecorderInfoAsync(string requestData);
    }
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    public interface IReceiverServiceResponseChannel : CayugaConnector.ServiceReference1.IReceiverServiceResponse, System.ServiceModel.IClientChannel {
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    public partial class ReceiverServiceResponseClient : System.ServiceModel.ClientBase<CayugaConnector.ServiceReference1.IReceiverServiceResponse>, CayugaConnector.ServiceReference1.IReceiverServiceResponse {
        
        public ReceiverServiceResponseClient() {
        }
        
        public ReceiverServiceResponseClient(string endpointConfigurationName) : 
                base(endpointConfigurationName) {
        }
        
        public ReceiverServiceResponseClient(string endpointConfigurationName, string remoteAddress) : 
                base(endpointConfigurationName, remoteAddress) {
        }
        
        public ReceiverServiceResponseClient(string endpointConfigurationName, System.ServiceModel.EndpointAddress remoteAddress) : 
                base(endpointConfigurationName, remoteAddress) {
        }
        
        public ReceiverServiceResponseClient(System.ServiceModel.Channels.Binding binding, System.ServiceModel.EndpointAddress remoteAddress) : 
                base(binding, remoteAddress) {
        }
        
        public string SendAlarm(string requestData) {
            return base.Channel.SendAlarm(requestData);
        }
        
        public System.Threading.Tasks.Task<string> SendAlarmAsync(string requestData) {
            return base.Channel.SendAlarmAsync(requestData);
        }
        
        public string SendHeartBeat(string requestData) {
            return base.Channel.SendHeartBeat(requestData);
        }
        
        public System.Threading.Tasks.Task<string> SendHeartBeatAsync(string requestData) {
            return base.Channel.SendHeartBeatAsync(requestData);
        }
        
        public string GetNTPServerInfo(string requestData) {
            return base.Channel.GetNTPServerInfo(requestData);
        }
        
        public System.Threading.Tasks.Task<string> GetNTPServerInfoAsync(string requestData) {
            return base.Channel.GetNTPServerInfoAsync(requestData);
        }
        
        public string SendRecorderInfo(string requestData) {
            return base.Channel.SendRecorderInfo(requestData);
        }
        
        public System.Threading.Tasks.Task<string> SendRecorderInfoAsync(string requestData) {
            return base.Channel.SendRecorderInfoAsync(requestData);
        }
    }
}
