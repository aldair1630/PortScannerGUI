from P6f import CtoF

def main():
    try:
        C = float(input("Enter temperature in Celsius: "))
        F = CtoF(C)
        print(f"{C}°C is equal to {F}°F")
    except ValueError:
        print("Invalid input. Please enter a number.")

if __name__ == "__main__":
    main()
