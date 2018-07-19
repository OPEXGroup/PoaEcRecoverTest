pragma solidity ^0.4.18;

contract Test {
    address public owner;

    constructor() public {
        owner = msg.sender;
    }

    function callEcRecover(bytes32 message, uint8 v, bytes32 r, bytes32 s) public view returns (address) {
        address signer = ecrecover(message, v, r, s);
        return signer;
    }
}