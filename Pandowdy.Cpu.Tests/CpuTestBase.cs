// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.Cpu.Tests;

/// <summary>
/// Base class for CPU instruction tests using the instance-based CPU API.
/// Each CPU variant should have its own derived test class.
/// </summary>
public abstract class CpuTestBase
{
    protected const ushort ProgramStart = 0x0400;
    protected const ushort IrqHandler = 0x0500;
    protected const ushort NmiHandler = 0x0600;

    /// <summary>
    /// The CPU variant to test. Override in derived classes.
    /// </summary>
    protected abstract CpuVariant Variant { get; }

    protected TestRamBus Bus { get; private set; } = null!;
    protected IPandowdyCpu Cpu { get; private set; } = null!;
    protected CpuState State { get; private set; } = null!;

    protected void SetupCpu()
    {
        SetupCpu(Variant);
    }

    protected void SetupCpu(CpuVariant variant)
    {
        Bus = new TestRamBus();
        State = new CpuState();
        Cpu = CpuFactory.Create(variant, State);
        Bus.SetResetVector(ProgramStart);
        Bus.SetIrqVector(IrqHandler);
        Bus.SetNmiVector(NmiHandler);
        Cpu.Reset(Bus);
    }

    /// <summary>
    /// Loads a program and sets up the CPU to execute from ProgramStart.
    /// </summary>
    protected void LoadAndReset(params byte[] program)
    {
        SetupCpu();
        Bus.LoadProgram(ProgramStart, program);
        Cpu.Reset(Bus);
    }

    /// <summary>
    /// Steps through one complete instruction and returns the number of cycles taken.
    /// Uses the variant specified by the derived class.
    /// </summary>
    protected int StepInstruction()
    {
        return Cpu.Step(Bus);
    }

    /// <summary>
    /// Steps through one complete instruction with an explicit variant override.
    /// </summary>
    protected int StepInstruction(CpuVariant variant)
    {
        if (Cpu.Variant != variant)
        {
            Cpu = CpuFactory.Create(variant, Cpu.State);
        }

        return Cpu.Step(Bus);
    }

    /// <summary>
    /// Executes cycles until the instruction completes, returning the cycle count.
    /// </summary>
    protected int ExecuteInstruction()
    {
        return ExecuteInstruction(Variant);
    }

    /// <summary>
    /// Executes cycles until the instruction completes, returning the cycle count.
    /// </summary>
    protected int ExecuteInstruction(CpuVariant variant)
    {
        if (Cpu.Variant != variant)
        {
            Cpu = CpuFactory.Create(variant, Cpu.State);
        }

        int cycles = 0;
        bool complete = false;
        while (!complete)
        {
            complete = Cpu.Clock(Bus);
            cycles++;
        }
        return cycles;
    }

    /// <summary>
    /// Gets the current CPU state.
    /// </summary>
    protected CpuState CurrentState => Cpu.State;

    /// <summary>
    /// Helper to set a value in zero page.
    /// </summary>
    protected void SetZeroPage(byte address, byte value)
    {
        Bus.Memory[address] = value;
    }

    /// <summary>
    /// Helper to set a 16-bit pointer in zero page (little-endian).
    /// </summary>
    protected void SetZeroPagePointer(byte address, ushort pointer)
    {
        Bus.Memory[address] = (byte)(pointer & 0xFF);
        Bus.Memory[(byte)(address + 1)] = (byte)(pointer >> 8);
    }

    /// <summary>
    /// Helper to set a value at an absolute address.
    /// </summary>
    protected void SetMemory(ushort address, byte value)
    {
        Bus.Memory[address] = value;
    }
}
